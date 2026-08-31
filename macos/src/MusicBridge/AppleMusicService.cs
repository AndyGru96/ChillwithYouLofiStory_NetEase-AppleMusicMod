using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace MusicBridge;

internal static class AppleMusicService
{
	private struct AmJob
	{
		public Action Work;

		public Action OnError;
	}

	public static AmConnState ConnState = AmConnState.Disconnected;

	public static string StatusText = "";

	public static string AccountName;

	public static readonly List<AmPlaylist> Playlists = new List<AmPlaylist>();

	public static bool PlaylistsLoading;

	public static string PlaylistsError;

	public static string ScanProgress;

	public static SmtcSnapshot NowPlaying = new SmtcSnapshot();

	private static SmtcSnapshot _anchorSnap;

	private static float _anchorAt;

	private static double _anchorPos;

	private static Thread _worker;

	private static readonly object Gate = new object();

	private static readonly Queue<AmJob> Jobs = new Queue<AmJob>();

	private static volatile bool _stop;

	private static int _pollPending;

	private static int _operationGeneration;

	private static bool _autoSyncAfterConnect;

	private static int _playlistsLoadingOwner;

	public static AmPlaylist CurrentPlaylist;

	public static int CurrentIndex = -1;

	public static bool Shuffle;

	public static bool RepeatOne;

	private static readonly System.Random Rng = new System.Random();

	private static readonly List<int> ShuffleHistory = new List<int>();

	private static long _queueTransitionGuardUntilUtcTicks;

	private static bool _queueWasPlaying;

	private static string _queueObservedTrackKey;

	public static string HintText;

	private static float _nextPollAt;

	private static volatile string _lastStatusSignature;

	private static readonly int[] FastPollOffsetsMs = new int[4] { 100, 250, 500, 900 };

	private static int _fastStep = -1;

	private static long _fastAtTicks;

	private static string _fastBaseline;

	private static readonly object FastGate = new object();

	public static Sprite CoverSprite;



	private static string _lyricsForTitle;

	private static string _lyricsRequestKey;

	private static bool _lyricsRequestStarted;

	private static float _lyricsRetryAt;

	private static string _waitingDurationFor;

	public static float Volume = -1f;

	private static float _pendingVolume = -1f;

	private static float _lastVolumeSentAt;

	public static int ShuffleState = -1;

	public static bool HasTrack
	{
		get
		{
			SmtcSnapshot nowPlaying = NowPlaying;
			if (nowPlaying != null && nowPlaying.Valid)
			{
				return !string.IsNullOrEmpty(nowPlaying.Title);
			}
			return false;
		}
	}

	public static bool IsPlaying
	{
		get
		{
			SmtcSnapshot nowPlaying = NowPlaying;
			if (nowPlaying != null && nowPlaying.Valid)
			{
				return nowPlaying.IsPlaying;
			}
			return false;
		}
	}

	public static double Duration
	{
		get
		{
			SmtcSnapshot nowPlaying = NowPlaying;
			if (nowPlaying == null || !nowPlaying.Valid)
			{
				return 0.0;
			}
			return nowPlaying.DurationSeconds;
		}
	}

	public static bool UserDisconnected { get; private set; }

	public static double GetPosition()
	{
		SmtcSnapshot nowPlaying = NowPlaying;
		if (nowPlaying == null || !nowPlaying.Valid)
		{
			return 0.0;
		}
		if (nowPlaying != _anchorSnap)
		{
			_anchorSnap = nowPlaying;
			_anchorAt = Time.unscaledTime;
			_anchorPos = nowPlaying.PositionSeconds + MusicBridgeOptions.Current.Lyrics.PositionQuantizationCenterSeconds;
		}
		if (!nowPlaying.IsPlaying)
		{
			return _anchorPos;
		}
		double num = _anchorPos + (double)(Time.unscaledTime - _anchorAt);
		if (nowPlaying.DurationSeconds > 0.0 && num > nowPlaying.DurationSeconds)
		{
			num = nowPlaying.DurationSeconds;
		}
		return num;
	}

	private static void EnsureWorker()
	{
		if (_worker == null || !_worker.IsAlive)
		{
			_stop = false;
			_worker = new Thread(WorkerLoop);
			_worker.IsBackground = true;
			// macOS worker: no Windows COM apartment.
			_worker.Name = "MusicBridge_AppleMusic";
			_worker.Start();
		}
	}

	private static void WorkerLoop()
	{
		try
		{
			while (!_stop)
			{
				AmJob amJob = default(AmJob);
				lock (Gate)
				{
					if (Jobs.Count > 0)
					{
						amJob = Jobs.Dequeue();
					}
				}
				if (amJob.Work == null)
				{
					Thread.Sleep(MusicBridgeOptions.Current.Apple.WorkerIdlePollInterval);
					continue;
				}
				try
				{
					amJob.Work();
				}
				catch (Exception ex)
				{
					BridgeLog.Error("[AM] 后台任务异常：" + ex.Message);
					if (amJob.OnError != null)
					{
						try
						{
							amJob.OnError();
						}
						catch (Exception ex2)
						{
							BridgeLog.Error("[AM] 任务异常善后失败：" + ex2.Message);
						}
					}
				}
			}
		}
		catch (Exception ex3)
		{
			BridgeLog.Error("[AM] 后台线程崩溃：" + ex3);
		}
		finally
		{


		}
	}

	private static void Post(Action job)
	{
		Post(job, null);
	}

	private static void Post(Action job, Action onError)
	{
		if (job == null)
		{
			return;
		}
		EnsureWorker();
		lock (Gate)
		{
			Jobs.Enqueue(new AmJob
			{
				Work = job,
				OnError = onError
			});
		}
	}

	private static void Notify()
	{
		Plugin.RunOnMainThread(delegate
		{
			AppleMusicPanelUi.RequestRebuild();
		});
	}

	public static void Shutdown()
	{
		_stop = true;
	}

	public static void Disconnect()
	{
		Interlocked.Increment(ref _operationGeneration);
		UserDisconnected = true;
		ConnState = AmConnState.Disconnected;
		StatusText = "";
		HintText = null;
		AccountName = null;
		NowPlaying = new SmtcSnapshot();
		lock (Gate)
		{
			Playlists.Clear();
		}
		PlaylistsError = null;
		_playlistsLoadingOwner = 0;
		PlaylistsLoading = false;
		ScanProgress = null;
		BridgeLog.Info("[AM] 用户断开连接（Apple Music 本身不受影响，缓存保留）。");
		Notify();
	}

    public static void BeginConnect(bool force)
    {
        if (!force && (ConnState == AmConnState.Connected || ConnState == AmConnState.Connecting)) return;
        UserDisconnected = false;
        int generation = Interlocked.Increment(ref _operationGeneration);
        ConnState = AmConnState.Connecting;
        StatusText = "正在连接 macOS 音乐…";
        Notify();
        Post(delegate {
            try {
                MacMusicBackend.Connect();
                if (generation != _operationGeneration) return;
                AccountName = "macOS 音乐资料库";
                ConnState = AmConnState.Connected;
                StatusText = "";
                Notify();
                if (_autoSyncAfterConnect || !LoadPlaylists(false)) { _autoSyncAfterConnect = false; SyncLibrary(); }
                ReadVolume();
            } catch (Exception ex) {
                if (generation != _operationGeneration) return;
                ConnState = AmConnState.Failed;
                StatusText = ex.Message;
                Notify();
            }
        });
    }

	public static bool LoadPlaylists(bool force)
	{
		if (PlaylistsLoading)
		{
			return true;
		}
		if (!force && Playlists.Count > 0)
		{
			return true;
		}
		List<AmPlaylist> list = AppleMusicCache.Load(AccountName);
		if (list != null && list.Count > 0)
		{
			lock (Gate)
			{
				Playlists.Clear();
				Playlists.AddRange(list);
			}
			PlaylistsError = null;

			Notify();
			return true;
		}
		lock (Gate)
		{
			Playlists.Clear();
		}
		string text = AppleMusicCache.CachedAccount();
		PlaylistsError = ((text != null && AccountName != null && text != AccountName) ? ("缓存属于账号「" + text + "」，请点「更新播放列表」重新读取。") : "还没有同步过。点「更新播放列表」读取你的资料库。");
		Notify();
		return false;
	}

	private static void ReleasePlaylistsLoading(int owner)
	{
		if (_playlistsLoadingOwner == owner)
		{
			_playlistsLoadingOwner = 0;
			PlaylistsLoading = false;
		}
	}

    public static void SyncLibrary()
    {
        if (PlaylistsLoading) return;
        if (ConnState != AmConnState.Connected) { _autoSyncAfterConnect = true; BeginConnect(false); return; }
        PlaylistsLoading = true;
        PlaylistsError = null;
        ScanProgress = "正在读取音乐资料库…";
        int generation = Interlocked.Increment(ref _operationGeneration);
        _playlistsLoadingOwner = generation;
        Notify();
        Post(delegate {
            try {
                var playlists = MacMusicBackend.Scan(name => { ScanProgress = "正在读取 · " + name; Notify(); },
                    () => _stop || generation != _operationGeneration);
                if (generation != _operationGeneration) return;
                AmValidation validation;
                if (!AppleMusicCache.Commit(playlists, AccountName, out validation))
                    throw new InvalidOperationException("歌单校验未通过；保留原有缓存。请重试。");
                lock (Gate) { Playlists.Clear(); Playlists.AddRange(playlists); }
            } catch (OperationCanceledException) { }
            catch (Exception ex) { if (generation == _operationGeneration) PlaylistsError = ex.Message; }
            finally {
                ReleasePlaylistsLoading(generation);
                if (generation == _operationGeneration) { ScanProgress = null; Notify(); }
            }
        });
    }

	public static void ToggleExpand(AmPlaylist pl)
	{
		if (pl == null)
		{
			return;
		}
		if (pl.Expanded)
		{
			pl.Expanded = false;
			Notify();
			return;
		}
		pl.Expanded = true;
		if (pl.IsFolder)
		{
			if (!pl.ChildrenLoaded)
			{
				pl.TracksError = "缓存里没有这个文件夹的内容，点上面的「刷新」重新扫描一次。";
			}
			Notify();
			return;
		}
		CollapseSiblings(pl);
		if (!pl.TracksComplete && pl.Tracks.Count == 0)
		{
			pl.TracksError = "这个歌单还没扫到，点上面的「刷新」重新完整扫描一次。";
		}
		Notify();
	}

	private static void CollapseSiblings(AmPlaylist keep)
	{
		CollapseIn(Playlists, keep);
	}

	private static void CollapseIn(List<AmPlaylist> list, AmPlaylist keep)
	{
		foreach (AmPlaylist item in list)
		{
			if (!item.IsFolder && item != keep)
			{
				item.Expanded = false;
			}
			if (item.Children.Count > 0)
			{
				CollapseIn(item.Children, keep);
			}
		}
	}

	public static void NextInQueue()
	{
		AmPlaylist currentPlaylist = CurrentPlaylist;
		if (currentPlaylist == null || currentPlaylist.Tracks.Count == 0)
		{
			Next();
			return;
		}
		int count = currentPlaylist.Tracks.Count;
		int num = (RepeatOne ? CurrentIndex : ((!Shuffle) ? ((CurrentIndex + 1) % count) : ((count > 1) ? PickOtherRandom(count, CurrentIndex) : 0)));
		if (num < 0 || num >= count)
		{
			num = 0;
		}
		PlayTrackInternal(currentPlaylist, currentPlaylist.Tracks[num], recordHistory: true);
	}

	public static void PreviousInQueue()
	{
		AmPlaylist currentPlaylist = CurrentPlaylist;
		if (currentPlaylist == null || currentPlaylist.Tracks.Count == 0)
		{
			Previous();
			return;
		}
		int count = currentPlaylist.Tracks.Count;
		bool recordHistory = true;
		int num;
		if (RepeatOne)
		{
			num = CurrentIndex;
		}
		else if (!Shuffle || ShuffleHistory.Count <= 0)
		{
			num = ((!Shuffle) ? ((CurrentIndex - 1 + count) % count) : ((count > 1) ? PickOtherRandom(count, CurrentIndex) : 0));
		}
		else
		{
			num = ShuffleHistory[ShuffleHistory.Count - 1];
			ShuffleHistory.RemoveAt(ShuffleHistory.Count - 1);
			recordHistory = false;
		}
		if (num < 0 || num >= count)
		{
			num = 0;
		}
		PlayTrackInternal(currentPlaylist, currentPlaylist.Tracks[num], recordHistory);
	}

	private static int PickOtherRandom(int count, int avoid)
	{
		for (int i = 0; i < 8; i++)
		{
			int num = Rng.Next(count);
			if (num != avoid)
			{
				return num;
			}
		}
		return (avoid + 1) % count;
	}

	public static void PlayTrack(AmPlaylist pl, AmTrack t)
	{
		PlayTrackInternal(pl, t, recordHistory: true);
	}

	private static void PlayTrackInternal(AmPlaylist pl, AmTrack t, bool recordHistory)
	{
		if (pl == null || t == null)
		{
			return;
		}
		int num = pl.Tracks.IndexOf(t);
		if (recordHistory && Shuffle && CurrentPlaylist == pl && CurrentIndex >= 0 && num >= 0 && num != CurrentIndex)
		{
			ShuffleHistory.Add(CurrentIndex);
		}
		if (CurrentPlaylist != pl)
		{
			ShuffleHistory.Clear();
		}
		CurrentPlaylist = pl;
		CurrentIndex = num;
		_queueTransitionGuardUntilUtcTicks = DateTime.UtcNow.Add(MusicBridgeOptions.Current.Apple.QueueTransitionGuard).Ticks;
		_queueWasPlaying = false;
		HintText = "正在起播 · " + t.Name;
		BridgePanel.ClaimAudio(MusicProvider.AppleMusic);
		Notify();
		int playGeneration = _operationGeneration;
        Post(delegate {
            if (playGeneration != _operationGeneration || ConnState != AmConnState.Connected) return;
            try {
                MacMusicBackend.Play(pl, t);
                HintText = null;
                PollNow();
            } catch (Exception ex) { HintText = "播放失败 · " + ex.Message; }
            finally { Notify(); }
        });
    }

	private static bool SnapshotMatchesTrack(SmtcSnapshot snap, AmTrack expected)
	{
		if (snap == null || !snap.Valid || expected == null)
		{
			return false;
		}
		if (!string.Equals(snap.Title, expected.Name, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		if (DurationText.TryParseSeconds(expected.DurationText, out var seconds) && snap.DurationSeconds > 0.0 && Math.Abs(seconds - snap.DurationSeconds) > MusicBridgeOptions.Current.Apple.MetadataDurationTolerance.TotalSeconds)
		{
			BridgeLog.History("[AM] 起播校验：曲名一致但时长差 " + Math.Abs(seconds - snap.DurationSeconds).ToString("F1") + "s，判定未换歌。");
			return false;
		}
		if (!string.IsNullOrWhiteSpace(expected.Artists) && !TextMatch.Contains(TextMatch.StripTrailingAlbum(snap.Artist), expected.Artists, MatchStrength.Loose) && !TextMatch.Contains(TextMatch.StripTrailingAlbum(snap.AlbumArtist), expected.Artists, MatchStrength.Loose))
		{
			BridgeLog.History("[AM] 起播校验：曲名与时长都对上了，但歌手对不上（行解析可能有偏差），按成功处理。");
		}
		return true;
	}



	private static bool TickFastPoll()
	{
		lock (FastGate)
		{
			if (_fastStep < 0)
			{
				return false;
			}
			if (_lastStatusSignature != _fastBaseline)
			{
				_fastStep = -1;
				return false;
			}
			if (DateTime.UtcNow.Ticks < _fastAtTicks)
			{
				return false;
			}
			int fastStep = _fastStep;
			if (fastStep + 1 < FastPollOffsetsMs.Length)
			{
				_fastStep = fastStep + 1;
				_fastAtTicks = DateTime.UtcNow.Ticks + 10000L * (long)(FastPollOffsetsMs[fastStep + 1] - FastPollOffsetsMs[fastStep]);
			}
			else
			{
				_fastStep = -1;
			}
		}
		return true;
	}

    public static void TickPolling(float now, bool panelVisible)
    {
        if (ConnState != AmConnState.Connected || !panelVisible || (!TickFastPoll() && now < _nextPollAt)) return;
        if (Interlocked.Exchange(ref _pollPending, 1) != 0) return;
        int generation = _operationGeneration;
        _nextPollAt = now + (float)MusicBridgeOptions.Current.Apple.VisibleStatusPollInterval.TotalSeconds;
        Post(delegate {
            try {
                var snap = MacMusicBackend.ReadSnapshot() ?? new SmtcSnapshot();
                Plugin.RunOnMainThread(delegate {
                    if (generation != _operationGeneration || ConnState != AmConnState.Connected) return;
                    if (snap.Valid) ObserveQueueTransition(snap);
                    NowPlaying = snap;
                    string signature = snap.Title + "\u0001" + snap.Artist + "\u0001" + snap.Status + "\u0001" + snap.Valid;
                    if (signature != _lastStatusSignature) { _lastStatusSignature = signature; Notify(); }
                });
            } catch (Exception ex) {
                BridgeLog.WarnThrottled("mac-music-poll", ex.Message, TimeSpan.FromMinutes(1));
                Plugin.RunOnMainThread(delegate {
                    if (generation == _operationGeneration) { NowPlaying = new SmtcSnapshot(); StatusText = ex.Message; Notify(); }
                });
            } finally { Interlocked.Exchange(ref _pollPending, 0); }
        });
    }

	private static void ObserveQueueTransition(SmtcSnapshot snap)
	{
		if (CurrentPlaylist == null || CurrentIndex < 0 || CurrentIndex >= CurrentPlaylist.Tracks.Count)
		{
			return;
		}
		string text = snap.Title + "\u0001" + snap.AlbumTitle + "\u0001" + snap.Artist;
		if (snap.IsPlaying)
		{
			_queueWasPlaying = true;
			_queueObservedTrackKey = text;
			return;
		}
		bool num = DateTime.UtcNow.Ticks >= _queueTransitionGuardUntilUtcTicks && _queueWasPlaying && snap.Status == 3 && (string.IsNullOrEmpty(_queueObservedTrackKey) || text == _queueObservedTrackKey);
		_queueWasPlaying = false;
		if (!num)
		{
			return;
		}
		AmPlaylist currentPlaylist = CurrentPlaylist;
		int currentIndex = CurrentIndex;
		if (RepeatOne)
		{
			BridgeLog.Info("[AM] 自然播完：单曲循环，重新播放当前歌曲。");
			PlayTrackInternal(currentPlaylist, currentPlaylist.Tracks[currentIndex], recordHistory: false);
			return;
		}
		if (Shuffle)
		{
			int num2 = ((currentPlaylist.Tracks.Count > 1) ? PickOtherRandom(currentPlaylist.Tracks.Count, currentIndex) : 0);
			BridgeLog.Info("[AM] 自然播完：随机模式选择第 " + (num2 + 1) + " 首。");
			PlayTrackInternal(currentPlaylist, currentPlaylist.Tracks[num2], recordHistory: true);
			return;
		}
		int num3 = currentIndex + 1;
		if (num3 >= currentPlaylist.Tracks.Count)
		{
			num3 = 0;
			HintText = "队列已播完 · 从头继续";
			BridgeLog.Info("[AM] 队列已播完，按现有默认行为从第一首继续。");
		}
		PlayTrackInternal(currentPlaylist, currentPlaylist.Tracks[num3], recordHistory: true);
	}

	public static void PollNow()
	{
		lock (FastGate)
		{
			_fastBaseline = _lastStatusSignature;
			_fastStep = 0;
			_fastAtTicks = DateTime.UtcNow.Ticks + 10000L * (long)FastPollOffsetsMs[0];
		}
	}

	public static void TickLyrics(float now, bool panelVisible)
	{
		if (!panelVisible || !HasTrack)
		{
			return;
		}
		if (NowPlaying.DurationSeconds <= 0.0)
		{
			if (_waitingDurationFor != NowPlaying.Title)
			{
				_waitingDurationFor = NowPlaying.Title;
				BridgeLog.History("[歌词] 『" + NowPlaying.Title + "』时长还没就绪，等 SMTC 填好再查。");
			}
			return;
		}
		string title = NowPlaying.Title;
		string text = title + "\u0001" + NowPlaying.AlbumTitle + "\u0001" + NowPlaying.Artist;
		if (text != _lyricsRequestKey)
		{
			_lyricsRequestKey = text;
			_lyricsRequestStarted = false;
			_lyricsRetryAt = 0f;
			_lyricsForTitle = null;
		}
		if (text == _lyricsForTitle)
		{
			return;
		}
		if (_lyricsRequestStarted && LyricsEngine.ContextKey == text)
		{
			if (LyricsEngine.State == LyricsState.Loading)
			{
				return;
			}
			if (!LyricsEngine.ShouldRetry)
			{
				_lyricsForTitle = text;
				return;
			}
			if (_lyricsRetryAt <= 0f)
			{
				_lyricsRetryAt = now + (float)MusicBridgeOptions.Current.Apple.LyricsRetryDelay.TotalSeconds;
				return;
			}
			if (now < _lyricsRetryAt)
			{
				return;
			}
			BridgeLog.Info("[歌词] 临时失败已等待 " + MusicBridgeOptions.Current.Apple.LyricsRetryDelay.TotalSeconds.ToString("F0") + " 秒，自动重试当前曲目。");
		}
		else
		{
			_lyricsRequestStarted = false;
		}
		_lyricsRequestStarted = true;
		_lyricsRetryAt = 0f;
		LyricsEngine.LoadBySearch(title, NowPlaying.Artist, NowPlaying.AlbumTitle, NowPlaying.DurationSeconds, text);
	}

	public static string CurrentLyricLine()
	{
		bool changed;
		return LyricsEngine.GetDisplayText(GetPosition(), out changed);
	}

	public static void SetVolume(float v)
	{
		Volume = Mathf.Clamp01(v);
		_pendingVolume = Volume;
	}

	public static void TickVolume(float now)
	{
		if (!(_pendingVolume < 0f) && !(now - _lastVolumeSentAt < 0.12f))
		{
			_lastVolumeSentAt = now;
			float v = _pendingVolume;
			_pendingVolume = -1f;
			Post(delegate
			{
				MacMusicBackend.SetVolume(v);
			});
		}
	}

	public static void ReadVolume()
	{
		Post(delegate
		{
			float volume = MacMusicBackend.GetVolume();
			if (volume >= 0f)
			{
				Volume = volume;
				Notify();
			}
		});
	}

    public static void ToggleShuffle() { Shuffle = !Shuffle; ShuffleState = Shuffle ? 1 : 0; Notify(); }

	public static void PauseIfPlaying()
	{
		if (IsPlaying)
		{
			Post(delegate
			{
				MacMusicBackend.Command("pause");
			});
			PollNow();
		}
	}

	public static void TogglePlayPause()
	{
		Post(delegate
		{
			MacMusicBackend.Command("toggle");
		});
		PollNow();
	}

	public static void Next()
	{
		Post(delegate
		{
			MacMusicBackend.Command("next");
		});
		PollNow();
	}

	public static void Previous()
	{
		Post(delegate
		{
			MacMusicBackend.Command("previous");
		});
		PollNow();
	}

	public static void Seek(double sec)
	{
		Post(delegate
		{
			MacMusicBackend.Seek(sec);
		});
		PollNow();
	}
}
