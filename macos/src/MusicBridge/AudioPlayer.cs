using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace MusicBridge;

internal sealed class AudioPlayer : MonoBehaviour
{
	private sealed class UrlLookupResult
	{
		public string Url;

		public string Failure;

		public volatile bool Done;
	}

	private static AudioPlayer _instance;

	private AudioSource _source;

	private int _generation;

	private Coroutine _loadRoutine;

	private UnityWebRequest _activeRequest;

	private NeteaseRequestCancellation _urlCancellation;

	private List<TrackInfo> _queue = new List<TrackInfo>();

	private int _index = -1;

	public bool RepeatQueue = true;

	public bool RepeatOne;

	public bool Shuffle;

	private readonly System.Random _rng = new System.Random();

	private readonly List<int> _shuffleHistory = new List<int>();

	private bool _wasPlaying;

	private float _trackStartedAt;

	private bool _sawPlaying;

	private float _lastGoodPosition;

	private int _resumeAttempts;

	private bool _playAfterLoad = true;

	private float _resumePositionAfterLoad;

	private const int MaximumResumeAttempts = 3;

	public static AudioPlayer Instance => _instance;

	public PlaybackState State { get; private set; }

	public TrackInfo CurrentTrack { get; private set; }

	public string LastError { get; private set; }

	public QueueSource Source { get; private set; }

	public string SourceName { get; private set; } = "";

	public int QueueIndex => _index;

	public int QueueCount => _queue.Count;

	public bool IsActive
	{
		get
		{
			if (CurrentTrack != null)
			{
				return State != PlaybackState.Idle;
			}
			return false;
		}
	}

	public float Volume
	{
		get
		{
			if (!(_source != null))
			{
				return 0f;
			}
			return _source.volume;
		}
		set
		{
			if (_source != null)
			{
				_source.volume = Mathf.Clamp01(value);
			}
		}
	}

	public float PositionSeconds
	{
		get
		{
			if (_source == null || _source.clip == null)
			{
				return 0f;
			}
			return _source.time;
		}
	}

	public float DurationSeconds
	{
		get
		{
			if (_source != null && _source.clip != null && _source.clip.length > 0f)
			{
				return _source.clip.length;
			}
			if (CurrentTrack != null && CurrentTrack.DurationMs > 0)
			{
				return (float)CurrentTrack.DurationMs / 1000f;
			}
			return 0f;
		}
	}

	public event Action StateChanged;

	public event Action<TrackInfo> TrackChanged;

	public static void Initialize()
	{
		if (!(_instance != null))
		{
			GameObject obj = new GameObject("MusicBridge_AudioPlayer");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			obj.hideFlags = HideFlags.HideAndDontSave;
			_instance = obj.AddComponent<AudioPlayer>();
			BridgeLog.Info("MusicBridge 播放器已创建（独立 AudioSource，不影响游戏音频系统）。");
		}
	}

	private void Awake()
	{
		try
		{
			RepeatQueue = MusicBridgeOptions.Current.Netease.RepeatQueue;
		}
		catch
		{
		}
		_source = base.gameObject.AddComponent<AudioSource>();
		_source.playOnAwake = false;
		_source.loop = false;
		_source.volume = 0.7f;
		_source.spatialBlend = 0f;
		_source.bypassEffects = true;
		_source.bypassListenerEffects = true;
		_source.bypassReverbZones = true;
		_source.ignoreListenerPause = true;
		_source.ignoreListenerVolume = false;
	}

	public void PlayQueue(IList<TrackInfo> tracks, int startIndex, QueueSource source, string sourceName)
	{
		if (tracks != null && tracks.Count != 0)
		{
			_queue = new List<TrackInfo>(tracks);
			_shuffleHistory.Clear();
			_index = Mathf.Clamp(startIndex, 0, _queue.Count - 1);
			Source = source;
			SourceName = sourceName ?? "";
			BridgeLog.History("建立播放队列：来源=" + source.ToString() + "『" + SourceName + "』共 " + _queue.Count + " 首，从第 " + (_index + 1) + " 首开始。");
			PlayIndex(_index);
		}
	}

	private bool IsPlayableAt(int i)
	{
		return _queue[i]?.Playable ?? false;
	}

	private int FirstPlayableFrom(int start, bool wrap, out int skipped)
	{
		return PlaylistAssembly.FirstPlayable(_queue.Count, start, wrap, forward: true, IsPlayableAt, out skipped);
	}

	private int FirstPlayableBefore(int start, bool wrap, out int skipped)
	{
		return PlaylistAssembly.FirstPlayable(_queue.Count, start, wrap, forward: false, IsPlayableAt, out skipped);
	}

	private void StopNoPlayable(int skipped)
	{
		AbortActiveRequest();
		CancelUrlLookup();
		_generation++;
		if (_source != null)
		{
			_source.Stop();
		}
		ReleaseClip();
		_sawPlaying = false;
		_lastGoodPosition = 0f;
		LastError = "队列里接下来没有可播放的曲目（已跳过 " + skipped + " 首不可播放）";
		State = PlaybackState.Failed;
		BridgeLog.Info("连续 " + skipped + " 首不可播放，队列中已无可播放曲目，停止。");
		Notify();
	}

	public void Next()
	{
		if (_queue.Count == 0)
		{
			return;
		}
		if (Shuffle && _queue.Count > 1)
		{
			int num = _index;
			for (int i = 0; i < 8; i++)
			{
				if (num != _index)
				{
					break;
				}
				num = _rng.Next(_queue.Count);
			}
			if (num == _index)
			{
				num = (_index + 1) % _queue.Count;
			}
			int skipped;
			int num2 = FirstPlayableFrom(num, wrap: true, out skipped);
			if (num2 < 0)
			{
				StopNoPlayable(skipped);
				return;
			}
			if (skipped > 0)
			{
				BridgeLog.Info("随机播放：跳过 " + skipped + " 首不可播放曲目。");
			}
			BridgeLog.History("随机播放：跳到队列第 " + (num2 + 1) + " 首。");
			if (_index >= 0)
			{
				_shuffleHistory.Add(_index);
				if (_shuffleHistory.Count > 256)
				{
					_shuffleHistory.RemoveRange(0, 128);
				}
			}
			PlayIndex(num2);
			return;
		}
		int num3 = _index + 1;
		if (num3 >= _queue.Count)
		{
			if (!RepeatQueue)
			{
				Stop();
				BridgeLog.Info("已到队列末尾，停止播放。");
				return;
			}
			num3 = 0;
		}
		int skipped2;
		int num4 = FirstPlayableFrom(num3, RepeatQueue, out skipped2);
		if (num4 < 0)
		{
			if (skipped2 > 0)
			{
				StopNoPlayable(skipped2);
				return;
			}
			Stop();
			BridgeLog.Info("已到队列末尾，停止播放。");
		}
		else
		{
			if (skipped2 > 0)
			{
				BridgeLog.Info("自动续播：跳过 " + skipped2 + " 首不可播放曲目（无版权 / 需 VIP 等）。");
			}
			PlayIndex(num4);
		}
	}

	public void Previous()
	{
		if (_queue.Count == 0)
		{
			return;
		}
		if (State == PlaybackState.Playing && PositionSeconds > 3f)
		{
			Seek(0f);
			return;
		}
		int index;
		if (Shuffle && _shuffleHistory.Count > 0)
		{
			index = _shuffleHistory[_shuffleHistory.Count - 1];
			_shuffleHistory.RemoveAt(_shuffleHistory.Count - 1);
			PlayIndex(index);
			return;
		}
		index = _index - 1;
		if (index < 0)
		{
			if (!RepeatQueue)
			{
				Seek(0f);
				return;
			}
			index = _queue.Count - 1;
		}
		int skipped;
		int num = FirstPlayableBefore(index, RepeatQueue, out skipped);
		if (num < 0)
		{
			if (skipped > 0)
			{
				BridgeLog.Info("上一首：往前 " + skipped + " 首都不可播放，停留在当前曲目。");
			}
			Seek(0f);
		}
		else
		{
			if (skipped > 0)
			{
				BridgeLog.Info("上一首：跳过 " + skipped + " 首不可播放曲目。");
			}
			PlayIndex(num);
		}
	}

	private void PlayIndex(int index)
	{
		if (index >= 0 && index < _queue.Count)
		{
			_index = index;
			PlayTrack(_queue[index]);
		}
	}

	public void PlayTrack(TrackInfo track)
	{
		if (track == null)
		{
			return;
		}
		PlaybackCoordinator.Claim(MusicProvider.Netease);
		if (CurrentTrack != null && CurrentTrack.Id == track.Id && (State == PlaybackState.Loading || State == PlaybackState.Playing))
		{
			BridgeLog.Info("忽略重复播放请求：同一首歌已在 " + State.ToString() + "。");
			return;
		}
		if (CurrentTrack != null && CurrentTrack.Id == track.Id && State == PlaybackState.Paused && _source != null && _source.clip != null)
		{
			_source.UnPause();
			State = PlaybackState.Playing;
			BridgeLog.History("同一首暂停曲目直接恢复，不重新下载：" + track.Name);
			Notify();
			return;
		}
		int gen = ++_generation;
		AbortActiveRequest();
		CancelUrlLookup();
		CurrentTrack = track;
		LastError = null;
		State = PlaybackState.Loading;
		Notify();
		if (this.TrackChanged != null)
		{
			this.TrackChanged(track);
		}
		if (!track.Playable)
		{
			LastError = (string.IsNullOrEmpty(track.UnplayableReason) ? "该歌曲不可播放" : track.UnplayableReason);
			State = PlaybackState.Failed;
			BridgeLog.Info("该曲目不可播放 " + track.Id + "：" + LastError + "（直接点选，不自动跳过）");
			Notify();
		}
		else
		{
			if (_loadRoutine != null)
			{
				StopCoroutine(_loadRoutine);
				_loadRoutine = null;
			}
			_loadRoutine = StartCoroutine(LoadAndPlay(track, gen));
		}
	}

	internal bool Owns(AudioSource source) => source == _source;

	internal Action CaptureOutputRecovery()
	{
		if (CurrentTrack == null || (State != PlaybackState.Playing && State != PlaybackState.Paused && State != PlaybackState.Loading)) return null;
		int generation = _generation;
		TrackInfo track = CurrentTrack;
		float position = State == PlaybackState.Loading ? _resumePositionAfterLoad : PositionSeconds;
		return () =>
		{
			if (_generation != generation || CurrentTrack != track) return;
			bool play = (State == PlaybackState.Loading ? _playAfterLoad : State != PlaybackState.Paused) && PlaybackCoordinator.Active == MusicProvider.Netease;
			AbortActiveRequest();
			CancelUrlLookup();
			if (_loadRoutine != null) StopCoroutine(_loadRoutine);
			_source.Stop();
			ReleaseClip();
			_sawPlaying = false;
			_lastGoodPosition = position;
			State = PlaybackState.Loading;
			int gen = ++_generation;
			_loadRoutine = StartCoroutine(LoadAndPlay(track, gen, position, play));
			Notify();
		};
	}

	private IEnumerator LoadAndPlay(TrackInfo track, int gen, float resumePosition = 0f, bool playAfterLoad = true)
	{
		_playAfterLoad = playAfterLoad;
		_resumePositionAfterLoad = resumePosition;
		string uri;
		bool fromCache = AudioDiskCache.TryGetUri(track.Id, out uri);
		if (fromCache)
		{
			BridgeLog.History("命中音频磁盘缓存 songId=" + track.Id);
		}
		UrlLookupResult lookup = new UrlLookupResult
		{
			Url = uri,
			Done = fromCache
		};
		NeteaseRequestCancellation cancellation = new NeteaseRequestCancellation();
		_urlCancellation = cancellation;
		Thread thread = new Thread((ThreadStart)delegate
		{
			try
			{
				lookup.Url = NeteaseApi.GetSongUrl(track.Id, out lookup.Failure, out var _, cancellation);
			}
			catch (Exception ex4)
			{
				lookup.Failure = "取播放地址异常：" + ex4.Message;
			}
			finally
			{
				lookup.Done = true;
			}
		});
		thread.IsBackground = true;
		if (!fromCache)
		{
			thread.Start();
		}
		while (!lookup.Done)
		{
			if (gen != _generation)
			{
				cancellation.Cancel();
				yield break;
			}
			yield return null;
		}
		if (_urlCancellation == cancellation)
		{
			_urlCancellation = null;
		}
		uri = lookup.Url;
		if (gen != _generation)
		{
			BridgeLog.Info("取址后发现已换歌（世代 " + gen + " != " + _generation + "），放弃本次加载。");
			yield break;
		}
		if (string.IsNullOrEmpty(uri))
		{
			LastError = lookup.Failure ?? "无法获取播放地址";
			State = PlaybackState.Failed;
			Notify();
			yield break;
		}
		BridgeLog.History("准备下载音频 songId=" + track.Id + "（协程存活，世代 " + gen + "）");
		UnityWebRequest req = null;
		UnityWebRequestAsyncOperation op = null;
		string text = null;
		try
		{
			req = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.MPEG);
			if (req.downloadHandler is DownloadHandlerAudioClip downloadHandlerAudioClip)
			{
				downloadHandlerAudioClip.streamAudio = false;
			}
			else
			{
				text = "downloadHandler 不是 DownloadHandlerAudioClip（音频模块可能被裁剪）";
			}
			req.timeout = (int)Math.Ceiling(MusicBridgeOptions.Current.Netease.AudioRequestTimeout.TotalSeconds);
			_activeRequest = req;
			op = req.SendWebRequest();
		}
		catch (Exception ex)
		{
			text = ex.GetType().Name + ": " + ex.Message;
		}
		if (text != null || req == null || op == null)
		{
			LastError = "无法创建音频请求：" + (text ?? "未知原因");
			State = PlaybackState.Failed;
			BridgeLog.Error("创建音频请求失败 songId=" + track.Id + " -> " + LastError);
			if (req != null)
			{
				try
				{
					req.Dispose();
				}
				catch
				{
				}
			}
			_activeRequest = null;
			Notify();
			yield break;
		}
		using (req)
		{
			BridgeLog.History("下载请求已发出 songId=" + track.Id);
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			float lastLog = realtimeSinceStartup;
			ulong lastBytes = 0uL;
			float lastProgressAt = realtimeSinceStartup;
			while (!op.isDone)
			{
				if (gen != _generation)
				{
					req.Abort();
					_activeRequest = null;
					yield break;
				}
				float realtimeSinceStartup2 = Time.realtimeSinceStartup;
				ulong downloadedBytes = req.downloadedBytes;
				if (downloadedBytes != lastBytes)
				{
					lastBytes = downloadedBytes;
					lastProgressAt = realtimeSinceStartup2;
				}
				if (realtimeSinceStartup2 - lastLog >= 2f)
				{
					lastLog = realtimeSinceStartup2;
					BridgeLog.History("音频下载中 songId=" + track.Id + "：已接收 " + downloadedBytes + " 字节，进度 " + (req.downloadProgress * 100f).ToString("0") + "%");
				}
				if (realtimeSinceStartup2 - lastProgressAt > (float)MusicBridgeOptions.Current.Netease.AudioStallTimeout.TotalSeconds)
				{
					req.Abort();
					_activeRequest = null;
					LastError = "音频下载停滞（" + MusicBridgeOptions.Current.Netease.AudioStallTimeout.TotalSeconds + " 秒无数据）";
					State = PlaybackState.Failed;
					BridgeLog.Warn("音频下载停滞，已中止。songId=" + track.Id + "，已接收 " + downloadedBytes + " 字节。");
					Notify();
					yield break;
				}
				yield return null;
			}
			_activeRequest = null;
			if (gen != _generation)
			{
				yield break;
			}
			if (req.result != UnityWebRequest.Result.Success)
			{
				if (fromCache)
				{
					AudioDiskCache.Remove(track.Id);
					BridgeLog.Warn("音频缓存已损坏，改从网络重取。songId=" + track.Id);
					_activeRequest = null;
					if (gen == _generation)
					{
						_loadRoutine = StartCoroutine(LoadAndPlay(track, gen, resumePosition, _playAfterLoad));
					}
					yield break;
				}
				LastError = "音频下载失败：" + req.error;
				State = PlaybackState.Failed;
				BridgeLog.Warn("音频下载失败 songId=" + track.Id + "  result=" + req.result.ToString() + "  httpCode=" + req.responseCode + "  error=" + req.error);
				Notify();
				yield break;
			}
			BridgeLog.History("音频下载完成 songId=" + track.Id + "，HTTP " + req.responseCode + "，已接收 " + req.downloadedBytes + " 字节。");
			if (!fromCache)
			{
				try
				{
					AudioDiskCache.Store(track.Id, req.downloadHandler.data);
				}
				catch (Exception ex2)
				{
					BridgeLog.Warn("读取音频缓存字节失败：" + ex2.Message);
				}
			}
			AudioClip audioClip = null;
			try
			{
				audioClip = DownloadHandlerAudioClip.GetContent(req);
			}
			catch (Exception ex3)
			{
				BridgeLog.Warn("解码失败：" + ex3.Message);
			}
			if (gen != _generation)
			{
				if (audioClip != null)
				{
					UnityEngine.Object.Destroy(audioClip);
				}
				yield break;
			}
			if (audioClip == null)
			{
				LastError = "音频解码失败";
				State = PlaybackState.Failed;
				Notify();
				yield break;
			}
			ReleaseClip();
			audioClip.name = "MB_" + track.Id;
			_source.clip = audioClip;
			_source.time = Mathf.Clamp(resumePosition, 0f, Mathf.Max(0f, audioClip.length - 0.05f));
			_source.Play();
			bool shouldPlay = _playAfterLoad && PlaybackCoordinator.Active == MusicProvider.Netease;
			if (!shouldPlay) _source.Pause();
			_trackStartedAt = Time.realtimeSinceStartup;
			_sawPlaying = false;
			_lastGoodPosition = _source.time;
			State = shouldPlay ? PlaybackState.Playing : PlaybackState.Paused;
			int num = Mathf.RoundToInt(audioClip.length * 1000f);
			if (num > 0 && Mathf.Abs(num - track.DurationMs) > 1000)
			{
				BridgeLog.Info("时长以实际解码为准：元数据 " + track.DurationMs / 1000 + "s -> 实际 " + num / 1000 + "s（" + BridgeLog.Redact(track.Name) + "）");
				track.DurationMs = num;
				NeteasePanelUi.RefreshTrackDuration(track.Id, track.DurationText);
			}
			BridgeLog.History("开始播放：" + track.Name + " · " + track.Artists + "（时长 " + audioClip.length.ToString("0.0") + "s）");
			Notify();
		}
		_loadRoutine = null;
	}

	public void TogglePlayPause()
	{
		if (State == PlaybackState.Loading)
		{
			_playAfterLoad = !_playAfterLoad;
			BridgeLog.Info(_playAfterLoad ? "音频加载完成后播放。" : "音频加载完成后保持暂停。");
			Notify();
			return;
		}
		if (!(_source == null) && !(_source.clip == null))
		{
			if (State == PlaybackState.Playing)
			{
				_source.Pause();
				State = PlaybackState.Paused;
			}
			else if (State == PlaybackState.Paused)
			{
				_source.UnPause();
				State = PlaybackState.Playing;
			}
			BridgeLog.Info("播放状态 -> " + State);
			Notify();
		}
	}

	public void PauseIfPlaying()
	{
		if (State == PlaybackState.Loading) _playAfterLoad = false;
		if (!(_source == null) && !(_source.clip == null) && State == PlaybackState.Playing)
		{
			_source.Pause();
			State = PlaybackState.Paused;
			BridgeLog.Info("网易云让位：已暂停（保留进度）。");
			Notify();
		}
	}

	public void Seek(float seconds)
	{
		if (!(_source == null) && !(_source.clip == null))
		{
			float time = Mathf.Clamp(seconds, 0f, Mathf.Max(0f, _source.clip.length - 0.05f));
			_source.time = time;
			BridgeLog.Info("跳转到 " + time.ToString("0.0") + "s");
			Notify();
		}
	}

	public void Stop()
	{
		AbortActiveRequest();
		CancelUrlLookup();
		_generation++;
		if (_source != null)
		{
			_source.Stop();
		}
		ReleaseClip();
		State = PlaybackState.Idle;
		CurrentTrack = null;
		Notify();
	}

	private void AbortActiveRequest()
	{
		if (_activeRequest != null)
		{
			try
			{
				_activeRequest.Abort();
			}
			catch
			{
			}
			_activeRequest = null;
		}
	}

	private void CancelUrlLookup()
	{
		NeteaseRequestCancellation urlCancellation = _urlCancellation;
		_urlCancellation = null;
		urlCancellation?.Cancel();
	}

	private void ReleaseClip()
	{
		if (_source != null && _source.clip != null)
		{
			AudioClip clip = _source.clip;
			_source.clip = null;
			UnityEngine.Object.Destroy(clip);
		}
	}

	private void Update()
	{
		// A null output can keep advancing time. Do not skip through the queue while recovering.
		if (AudioOutputRecovery.OutputUnavailable) return;
		if (State == PlaybackState.Playing && _source != null && _source.clip != null)
		{
			if (_source.isPlaying)
			{
				_sawPlaying = true;
				_lastGoodPosition = _source.time;
				_resumeAttempts = 0;
			}
			else if (_sawPlaying)
			{
				float length = _source.clip.length;
				if (!(_lastGoodPosition >= length - 1f))
				{
					if (_resumeAttempts >= 3)
					{
						BridgeLog.Error("音频被外部中止且连续 " + 3 + " 次恢复失败，放弃本曲并尝试下一首。");
						_sawPlaying = false;
						_lastGoodPosition = 0f;
						_resumeAttempts = 0;
						Next();
					}
					else
					{
						_resumeAttempts++;
						BridgeLog.Warn("音频被外部中止，正在恢复播放：位置=" + _lastGoodPosition.ToString("0.00") + " 曲长=" + length.ToString("0.00") + " 已播=" + (Time.realtimeSinceStartup - _trackStartedAt).ToString("0.0") + "s，第 " + _resumeAttempts + " 次。");
						_source.time = Mathf.Clamp(_lastGoodPosition, 0f, Mathf.Max(0f, length - 0.05f));
						_source.Play();
						Notify();
					}
				}
				else if (RepeatOne)
				{
					BridgeLog.Info("单曲循环：当前曲目重新播放。");
					_sawPlaying = false;
					_lastGoodPosition = 0f;
					_source.time = 0f;
					_source.Play();
					Notify();
				}
				else
				{
					BridgeLog.Info("当前曲目播放结束，自动进入下一首。");
					_sawPlaying = false;
					_lastGoodPosition = 0f;
					Next();
				}
			}
		}
		bool flag = State == PlaybackState.Playing;
		if (flag != _wasPlaying)
		{
			_wasPlaying = flag;
			Notify();
		}
	}

	private void Notify()
	{
		try
		{
			if (this.StateChanged != null)
			{
				this.StateChanged();
			}
		}
		catch (Exception ex)
		{
			BridgeLog.Error("播放状态回调异常：" + ex.Message);
		}
	}

	private void OnApplicationQuit()
	{
		BridgeLog.Info("游戏退出：停止 MusicBridge 音频与下载。");
		AbortActiveRequest();
		CancelUrlLookup();
		_generation++;
		if (_source != null)
		{
			_source.Stop();
		}
	}

	private void OnDestroy()
	{
		AbortActiveRequest();
		CancelUrlLookup();
		_generation++;
		ReleaseClip();
	}
}
