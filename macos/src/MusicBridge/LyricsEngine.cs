using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

namespace MusicBridge;

internal static class LyricsEngine
{
	private static readonly Regex TimeTag = new Regex("\\[(\\d{1,3}):(\\d{1,2})(?:[.:](\\d{1,3}))?\\]", RegexOptions.Compiled);

	private static readonly object Gate = new object();

	private static int _generation;

	private static volatile LyricsState _state = LyricsState.Idle;

	private static long _trackId;

	private static volatile string _statusText = "歌词：未连接音乐服务";

	private static volatile bool _shouldRetry;

	private static volatile string _contextKey = "";

	private static volatile List<LyricLine> _lines = new List<LyricLine>();

	private static volatile int _currentIndex = -1;

	private static int DerivativeRejects;

	private static readonly string[] DerivativeMarkers = new string[16]
	{
		"cover", "翻唱", "翻奏", "伴奏", "instrumental", "karaoke", "卡拉ok", "remix", "混音", "live",
		"现场", "演唱会", "钢琴版", "吉他版", "改编", "纯音乐版"
	};

	public static LyricsState State => _state;

	public static long TrackId => Volatile.Read(ref _trackId);

	public static string StatusText => _statusText;

	public static bool ShouldRetry => _shouldRetry;

	public static string ContextKey => _contextKey;

	public static event Action Changed;

	private static void Notify()
	{
		Plugin.RunOnMainThread(delegate
		{
			try
			{
				if (LyricsEngine.Changed != null)
				{
					LyricsEngine.Changed();
				}
			}
			catch (Exception ex)
			{
				BridgeLog.Error("歌词回调异常：" + ex.Message);
			}
		});
	}

	public static void LoadBySearch(string title, string artist, string album, double duration, string contextKey)
	{
		int gen;
		lock (Gate)
		{
			gen = ++_generation;
		}
		_lines = new List<LyricLine>();
		_currentIndex = -1;
		_contextKey = contextKey ?? "";
		_shouldRetry = false;
		if (string.IsNullOrEmpty(title))
		{
			_state = LyricsState.Idle;
			Volatile.Write(ref _trackId, 0L);
			_statusText = "歌词：未播放";
			Notify();
			return;
		}
		if (NeteaseService.ConnState != NeteaseConnState.Connected)
		{
			_state = LyricsState.Failed;
			_shouldRetry = true;
			_statusText = "需要先登录网易云才能显示歌词";
			Notify();
			return;
		}
		_state = LyricsState.Loading;
		_statusText = "歌词加载中…";
		Notify();
		Thread thread = new Thread((ThreadStart)delegate
		{
			try
			{
				string text = artist ?? "";
				int num = text.IndexOf('—');
				if (num > 0)
				{
					text = text.Substring(0, num);
				}
				text = text.Trim();
				string text2 = (album ?? "").Trim();
				NeteaseSearchStatus searchFailure = NeteaseSearchStatus.Success;
				TrackInfo trackInfo = null;
				string why = null;
				List<TrackInfo> list = new List<TrackInfo>();
				HashSet<long> seen = new HashSet<long>();
				int num2 = 0;
				DerivativeRejects = 0;
				List<string> list2 = new List<string> { title };
				List<string> list3 = new List<string>();
				if (text.Length > 0)
				{
					list3.Add(text);
				}
				List<string> list4 = new List<string>();
				if (text2.Length > 0)
				{
					list4.Add(text2);
				}
				if (text2.Length > 0)
				{
					CollectInto(list, seen, title + " " + text2, gen, ref searchFailure);
					num2++;
					lock (Gate)
					{
						if (gen != _generation)
						{
							return;
						}
					}
				}
				CollectInto(list, seen, (text.Length > 0) ? (title + " " + text) : title, gen, ref searchFailure);
				num2++;
				lock (Gate)
				{
					if (gen != _generation)
					{
						return;
					}
				}
				trackInfo = PickExact(list, list2, list3, list4, duration, out why);
				if (trackInfo != null)
				{
					why = "直查命中（" + why + "）";
				}
				if (trackInfo == null)
				{
					bool transientError;
					AmNameSet amNameSet = ITunesNameHub.Resolve(title, text, duration, out transientError);
					if (transientError)
					{
						RecordSearchFailure(ref searchFailure, NeteaseSearchStatus.NetworkError);
					}
					lock (Gate)
					{
						if (gen != _generation)
						{
							return;
						}
					}
					List<string> list5 = new List<string>(list2);
					List<string> list6 = new List<string>(list3);
					List<string> list7 = new List<string>(list4);
					if (amNameSet != null)
					{
						MergeBack(list5, amNameSet.Titles);
						MergeBack(list6, amNameSet.Artists);
						MergeBack(list7, amNameSet.Albums);
					}
					List<string> list8 = new List<string>();
					if (amNameSet != null)
					{
						foreach (AmLocalizedName localizedName in amNameSet.LocalizedNames)
						{
							if (localizedName != null && !(TextMatch.Canon(localizedName.Title) == TextMatch.Canon(title)))
							{
								AddSearchQuery(list8, localizedName.Title);
								if (!string.IsNullOrWhiteSpace(localizedName.Artist))
								{
									AddSearchQuery(list8, localizedName.Title + " " + localizedName.Artist);
								}
							}
						}
					}
					foreach (string item in list5)
					{
						if (TextMatch.Canon(item) != TextMatch.Canon(title))
						{
							AddSearchQuery(list8, item);
						}
					}
					int maximumCrossLanguageQueries = MusicBridgeOptions.Current.Lyrics.MaximumCrossLanguageQueries;
					int num3 = 0;
					foreach (string item2 in list8)
					{
						if (num3 >= maximumCrossLanguageQueries)
						{
							break;
						}
						CollectInto(list, seen, item2, gen, ref searchFailure);
						num3++;
						num2++;
						lock (Gate)
						{
							if (gen != _generation)
							{
								return;
							}
						}
					}
					trackInfo = PickExact(list, list5, list6, list7, duration, out var why2);
					if (trackInfo != null)
					{
						why = "跨语言完全一致（" + why2 + "）";
					}
					else
					{
						trackInfo = PickByNameSet(list, list5, list6, list7, duration, out why2);
						if (trackInfo != null)
						{
							why = "跨语言打分（" + why2 + "）";
						}
						else
						{
							BridgeLog.Info("[歌词] 候选 " + list.Count + " 条（" + num2 + " 次查询），没有一条达到门槛" + ((DerivativeRejects > 0) ? ("；其中 " + DerivativeRejects + " 条被判为改编版（Live/翻唱/伴奏/Remix/Acoustic）排除") : "") + "。要看曲名与逐条依据，开 Debug.VerboseListeningHistory。");
						}
					}
				}
				lock (Gate)
				{
					if (gen != _generation)
					{
						return;
					}
				}
				if (trackInfo == null)
				{
					_state = LyricsState.Failed;
					_shouldRetry = searchFailure != NeteaseSearchStatus.Success;
					_statusText = ((searchFailure == NeteaseSearchStatus.NetworkError) ? "获取歌词失败：网络错误" : (ShouldRetry ? "获取歌词失败" : "没有找到匹配的歌词"));
					BridgeLog.History("[歌词] 『" + title + "』专辑『" + text2 + "』歌手『" + text + "』时长 " + duration.ToString("F0") + "s：三轮都没匹配上，不显示歌词。搜索状态=" + searchFailure.ToString() + "，允许重试=" + ShouldRetry + "。");
					Notify();
				}
				else
				{
					BridgeLog.History("[歌词] Apple Music『" + title + "』-> 网易云匹配『" + trackInfo.Name + "』(id=" + trackInfo.Id + "，依据 " + why + "：专辑『" + trackInfo.Album + "』歌手『" + trackInfo.Artists + "』别名『" + trackInfo.Alias + "』时长 " + ((double)trackInfo.DurationMs / 1000.0).ToString("F0") + "s / SMTC " + duration.ToString("F0") + "s)");
					LoadFor(trackInfo, contextKey);
				}
			}
			catch (Exception ex)
			{
				BridgeLog.Warn("[歌词] 搜索失败：" + ex.Message);
				lock (Gate)
				{
					if (gen != _generation)
					{
						return;
					}
				}
				_state = LyricsState.Failed;
				_shouldRetry = true;
				_statusText = "获取歌词失败";
				Notify();
			}
		});
		thread.IsBackground = true;
		thread.Start();
	}

	private static TrackInfo PickByNameSet(List<TrackInfo> songs, List<string> titles, List<string> artists, List<string> albums, double durationSeconds, out string why)
	{
		why = null;
		if (songs == null || songs.Count == 0)
		{
			return null;
		}
		TrackInfo result = null;
		int num = 0;
		double num2 = double.MaxValue;
		string text = null;
		bool flag = WantsDerivative(titles, albums);
		foreach (TrackInfo song in songs)
		{
			if (!flag && TextMatch.IsDerivativeVersion(song.Name, song.Album))
			{
				DerivativeRejects++;
				continue;
			}
			int num3 = 0;
			List<string> list = new List<string>();
			switch (Math.Max(TextMatch.Rate(song.Name, titles, MatchStrength.Canonical), TextMatch.RateMultiValue(song.Alias, titles, MatchStrength.Canonical)))
			{
			case 2:
				num3 += 2;
				list.Add("曲名一致");
				break;
			case 1:
				num3++;
				list.Add("曲名部分一致");
				break;
			}
			int num4 = Math.Max(TextMatch.RateArtists(song.Artists, artists), TextMatch.RateMultiValue(song.ArtistAlias, artists, MatchStrength.Canonical));
			bool flag2 = false;
			if (num4 == 0 && TextMatch.MentionsArtist(song.Name + " " + song.Alias, artists))
			{
				num4 = 1;
				flag2 = true;
			}
			switch (num4)
			{
			case 2:
				num3 += 2;
				list.Add("歌手一致");
				break;
			case 1:
				num3++;
				list.Add(flag2 ? "曲名注明原唱" : "歌手部分一致");
				break;
			}
			int num5 = TextMatch.Rate(song.Album, albums, MatchStrength.Canonical);
			switch (num5)
			{
			case 2:
				num3 += 2;
				list.Add("专辑一致");
				break;
			case 1:
				num3++;
				list.Add("专辑部分一致");
				break;
			}
			double num6 = double.MaxValue;
			if (durationSeconds > 0.0 && song.DurationMs > 0)
			{
				num6 = Math.Abs((double)song.DurationMs / 1000.0 - durationSeconds);
				if (num6 > MusicBridgeOptions.Current.Lyrics.ExactDurationTolerance.TotalSeconds)
				{
					continue;
				}
				list.Add("时长差" + num6.ToString("F1") + "s");
			}
			if ((num4 != 0 || num5 != 0) && num3 >= 3 && (num3 > num || (num3 == num && num6 < num2)))
			{
				result = song;
				num = num3;
				num2 = num6;
				text = string.Join("+", list.ToArray()) + "，共 " + num3 + " 分";
			}
		}
		why = text;
		return result;
	}

	private static bool WantsDerivative(List<string> titles, List<string> albums)
	{
		if (titles != null)
		{
			foreach (string title in titles)
			{
				if (TextMatch.IsDerivativeVersion(title, null))
				{
					return true;
				}
			}
		}
		if (albums != null)
		{
			foreach (string album in albums)
			{
				if (TextMatch.HasDerivativeMarker(album))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static TrackInfo PickExact(List<TrackInfo> songs, List<string> titles, List<string> artists, List<string> albums, double durationSeconds, out string why)
	{
		why = null;
		if (songs == null || songs.Count == 0 || durationSeconds <= 0.0)
		{
			return null;
		}
		bool flag = WantsDerivative(titles, albums);
		foreach (string title in titles)
		{
			string text = TextMatch.Canon(title);
			if (text.Length == 0)
			{
				continue;
			}
			for (int i = 0; i < 2; i++)
			{
				foreach (TrackInfo song in songs)
				{
					if (song.DurationMs <= 0)
					{
						continue;
					}
					if (!flag && TextMatch.IsDerivativeVersion(song.Name, song.Album))
					{
						DerivativeRejects++;
						continue;
					}
					double num = Math.Abs((double)song.DurationMs / 1000.0 - durationSeconds);
					if (num > MusicBridgeOptions.Current.Lyrics.ExactDurationTolerance.TotalSeconds || (TextMatch.Canon(song.Name) != text && !TextMatch.AliasExact(song.Alias, text)))
					{
						continue;
					}
					if (i == 0)
					{
						if (TextMatch.Rate(song.Album, albums, MatchStrength.Canonical) == 2)
						{
							why = "曲名『" + title + "』+专辑，时长差" + num.ToString("F1") + "s";
							return song;
						}
					}
					else if (TextMatch.RateArtists(song.Artists, artists) == 2 || TextMatch.RateMultiValue(song.ArtistAlias, artists, MatchStrength.Canonical) == 2)
					{
						why = "曲名『" + title + "』+歌手，时长差" + num.ToString("F1") + "s";
						return song;
					}
				}
			}
		}
		return null;
	}

	private static void MergeBack(List<string> list, List<string> add)
	{
		if (add == null)
		{
			return;
		}
		for (int i = 0; i < add.Count; i++)
		{
			string text = add[i];
			if (string.IsNullOrWhiteSpace(text))
			{
				continue;
			}
			bool flag = false;
			foreach (string item in list)
			{
				if (TextMatch.Canon(item) == TextMatch.Canon(text))
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				list.Add(text.Trim());
			}
		}
	}

	private static string SearchKeyword(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return "";
		}
		StringBuilder stringBuilder = new StringBuilder();
		int num = 0;
		string text = s.Normalize(NormalizationForm.FormKC).Normalize(NormalizationForm.FormD);
		foreach (char c in text)
		{
			switch (c)
			{
			case '(':
			case '[':
			case '【':
			case '（':
				num++;
				break;
			case ')':
			case ']':
			case '】':
			case '）':
				if (num > 0)
				{
					num--;
				}
				break;
			default:
				if (num <= 0)
				{
					stringBuilder.Append(c);
				}
				break;
			}
		}
		string text2 = stringBuilder.ToString().Trim();
		if (text2.Length <= 0)
		{
			return s.Trim();
		}
		return text2;
	}

	private static void AddSearchQuery(List<string> queries, string value)
	{
		string text = SearchKeyword(value);
		if (text.Length == 0)
		{
			return;
		}
		string text2 = TextMatch.Canon(text);
		foreach (string query in queries)
		{
			if (TextMatch.Canon(SearchKeyword(query)) == text2)
			{
				return;
			}
		}
		queries.Add(text);
	}

	private static void CollectInto(List<TrackInfo> pool, HashSet<long> seen, string keyword, int gen, ref NeteaseSearchStatus searchFailure)
	{
		List<TrackInfo> songs;
		List<PlaylistInfo> playlists;
		NeteaseSearchStatus status;
		bool flag = NeteaseApi.Search(SearchKeyword(keyword), NeteaseSearchType.Song, 30, 0, out songs, out playlists, out status);
		if (!flag)
		{
			RecordSearchFailure(ref searchFailure, status);
		}
		lock (Gate)
		{
			if (gen != _generation)
			{
				return;
			}
		}
		if (!flag || songs == null)
		{
			return;
		}
		foreach (TrackInfo item in songs)
		{
			if (seen.Add(item.Id))
			{
				pool.Add(item);
			}
		}
	}

	private static void RecordSearchFailure(ref NeteaseSearchStatus aggregate, NeteaseSearchStatus current)
	{
		if (current == NeteaseSearchStatus.Success)
		{
			return;
		}
		if (aggregate != NeteaseSearchStatus.Success)
		{
			switch (current)
			{
			case NeteaseSearchStatus.ServiceRejected:
				if (aggregate != NeteaseSearchStatus.ProtocolError)
				{
					return;
				}
				break;
			case NeteaseSearchStatus.NetworkError:
				break;
			default:
				return;
			}
		}
		aggregate = current;
	}

	public static void LoadFor(TrackInfo track)
	{
		LoadFor(track, (track == null) ? "" : ("netease:" + track.Id));
	}

	private static void LoadFor(TrackInfo track, string contextKey)
	{
		int gen;
		lock (Gate)
		{
			gen = ++_generation;
		}
		_lines = new List<LyricLine>();
		_currentIndex = -1;
		_contextKey = contextKey ?? "";
		_shouldRetry = false;
		if (track == null)
		{
			_state = LyricsState.Idle;
			Volatile.Write(ref _trackId, 0L);
			_statusText = "歌词：未播放";
			Notify();
			return;
		}
		Volatile.Write(ref _trackId, track.Id);
		_state = LyricsState.Loading;
		_statusText = "歌词加载中…";
		Notify();
		Thread thread = new Thread((ThreadStart)delegate
		{
			try
			{
				string lrc;
				string translated;
				bool isPureMusic;
				bool networkError;
				bool lyrics = NeteaseApi.GetLyrics(track.Id, out lrc, out translated, out isPureMusic, out networkError);
				lock (Gate)
				{
					if (gen != _generation)
					{
						BridgeLog.Info("歌词结果迟到，已丢弃。");
						return;
					}
				}
				if (!lyrics)
				{
					_state = LyricsState.Failed;
					_shouldRetry = true;
					_statusText = (networkError ? "歌词：网络错误" : "歌词：接口异常");
					Notify();
				}
				else if (isPureMusic)
				{
					_state = LyricsState.PureMusic;
					_statusText = "纯音乐，请欣赏";
					Notify();
				}
				else if (string.IsNullOrEmpty(lrc))
				{
					_state = LyricsState.None;
					_statusText = "当前歌曲暂无歌词";
					Notify();
				}
				else
				{
					List<LyricLine> list = Parse(lrc, translated);
					lock (Gate)
					{
						if (gen != _generation)
						{
							return;
						}
					}
					if (list.Count == 0)
					{
						_state = LyricsState.None;
						_statusText = "当前歌曲暂无歌词";
					}
					else
					{
						_lines = list;
						_currentIndex = -1;
						_state = LyricsState.Ready;
						BridgeLog.Info("歌词解析完成：" + list.Count + " 行（songId=" + track.Id + "）");
					}
					Notify();
				}
			}
			catch (Exception ex)
			{
				BridgeLog.Error("歌词线程异常：" + ex.GetType().Name);
				lock (Gate)
				{
					if (gen != _generation)
					{
						return;
					}
				}
				_state = LyricsState.Failed;
				_shouldRetry = true;
				_statusText = "歌词：解析失败";
				Notify();
			}
		});
		thread.IsBackground = true;
		thread.Name = "MusicBridge-Lyrics";
		thread.Start();
	}

	public static List<LyricLine> Parse(string lrc, string translated)
	{
		SortedDictionary<double, LyricLine> sortedDictionary = new SortedDictionary<double, LyricLine>();
		ParseInto(lrc, ReadOffset(lrc), sortedDictionary, isTranslation: false);
		if (!string.IsNullOrEmpty(translated))
		{
			ParseInto(translated, ReadOffset(translated), sortedDictionary, isTranslation: true);
		}
		List<LyricLine> list = new List<LyricLine>(sortedDictionary.Count);
		foreach (KeyValuePair<double, LyricLine> item in sortedDictionary)
		{
			if (!string.IsNullOrEmpty(item.Value.Text) || !string.IsNullOrEmpty(item.Value.Translation))
			{
				list.Add(item.Value);
			}
		}
		return list;
	}

	private static double ReadOffset(string lrc)
	{
		if (string.IsNullOrEmpty(lrc))
		{
			return 0.0;
		}
		Match match = Regex.Match(lrc, "\\[offset:\\s*([+-]?\\d+)\\s*\\]", RegexOptions.IgnoreCase);
		if (!match.Success || !int.TryParse(match.Groups[1].Value, out var result))
		{
			return 0.0;
		}
		return (double)result / 1000.0;
	}

	private static void ParseInto(string text, double offset, SortedDictionary<double, LyricLine> map, bool isTranslation)
	{
		string[] array = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
		foreach (string text2 in array)
		{
			if (string.IsNullOrEmpty(text2))
			{
				continue;
			}
			MatchCollection matchCollection = TimeTag.Matches(text2);
			if (matchCollection.Count == 0)
			{
				continue;
			}
			Match match = matchCollection[matchCollection.Count - 1];
			string text3 = text2.Substring(match.Index + match.Length).Trim();
			if (text3.Length == 0)
			{
				continue;
			}
			foreach (Match item in matchCollection)
			{
				int num = int.Parse(item.Groups[1].Value, CultureInfo.InvariantCulture);
				int num2 = int.Parse(item.Groups[2].Value, CultureInfo.InvariantCulture);
				double num3 = 0.0;
				if (item.Groups[3].Success)
				{
					string value = item.Groups[3].Value;
					double num4 = double.Parse(value, CultureInfo.InvariantCulture);
					num3 = ((value.Length == 1) ? (num4 / 10.0) : ((value.Length == 2) ? (num4 / 100.0) : (num4 / 1000.0)));
				}
				double num5 = (double)(num * 60 + num2) + num3 + offset;
				if (num5 < 0.0)
				{
					num5 = 0.0;
				}
				double key = FindTimestampKey(map, num5);
				if (!map.TryGetValue(key, out var value2))
				{
					value2 = new LyricLine
					{
						TimeSeconds = num5
					};
				}
				if (isTranslation)
				{
					value2.Translation = text3;
				}
				else
				{
					value2.Text = text3;
				}
				map[key] = value2;
			}
		}
	}

	private static double FindTimestampKey(SortedDictionary<double, LyricLine> map, double time)
	{
		double totalSeconds = MusicBridgeOptions.Current.Lyrics.TimestampMergeTolerance.TotalSeconds;
		foreach (double key in map.Keys)
		{
			if (Math.Abs(key - time) <= totalSeconds)
			{
				return key;
			}
			if (key > time + totalSeconds)
			{
				break;
			}
		}
		return time;
	}

	private static string MatchingDots()
	{
		int num = 1 + (int)(Time.unscaledTime * 2.5f % 3f);
		if (num < 1)
		{
			num = 1;
		}
		else if (num > 3)
		{
			num = 3;
		}
		return new string('。', num) + new string('\u3000', 3 - num);
	}

	public static string GetDisplayText(double position, out bool changed)
	{
		changed = false;
		if (State == LyricsState.Loading)
		{
			return "正在从网易云音乐获取歌词\u3000匹配中" + MatchingDots();
		}
		if (State != LyricsState.Ready || _lines.Count == 0)
		{
			return StatusText;
		}
		int num = -1;
		int num2 = 0;
		int num3 = _lines.Count - 1;
		double num4 = position + 0.02;
		while (num2 <= num3)
		{
			int num5 = num2 + (num3 - num2 >> 1);
			if (_lines[num5].TimeSeconds <= num4)
			{
				num = num5;
				num2 = num5 + 1;
			}
			else
			{
				num3 = num5 - 1;
			}
		}
		if (num != _currentIndex)
		{
			_currentIndex = num;
			changed = true;
		}
		if (num < 0)
		{
			return "";
		}
		LyricLine lyricLine = _lines[num];
		if (!string.IsNullOrEmpty(lyricLine.Translation) && !string.IsNullOrEmpty(lyricLine.Text))
		{
			return lyricLine.Text + "   /   " + lyricLine.Translation;
		}
		if (!string.IsNullOrEmpty(lyricLine.Text))
		{
			return lyricLine.Text;
		}
		return lyricLine.Translation;
	}

	public static void Reset()
	{
		lock (Gate)
		{
			_generation++;
		}
		_lines = new List<LyricLine>();
		_currentIndex = -1;
		_state = LyricsState.Idle;
		Volatile.Write(ref _trackId, 0L);
		_shouldRetry = false;
		_contextKey = "";
		_statusText = "歌词：未播放";
		Notify();
	}
}
