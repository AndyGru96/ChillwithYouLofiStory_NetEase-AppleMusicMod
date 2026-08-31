using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;

namespace MusicBridge;

internal static class NeteaseApi
{
	private const string Origin = "https://music.163.com";

	private const string PrimarySearchPath = "/weapi/cloudsearch/pc";

	private const string FallbackSearchPath = "/weapi/search/get";

	private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

	private static CookieContainer _cookies;

	private static readonly object CookieLock;

	static NeteaseApi()
	{
		_cookies = NewContainer();
		CookieLock = new object();
		try
		{
			ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
		}
		catch
		{
			try
			{
				ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
			}
			catch
			{
			}
		}
		try
		{
			ServicePointManager.DefaultConnectionLimit = MusicBridgeOptions.Current.Netease.ServicePointConnectionLimit;
		}
		catch
		{
		}
	}

	private static CookieContainer NewContainer()
	{
		CookieContainer cookieContainer = new CookieContainer();
		Uri uri = new Uri("https://music.163.com");
		cookieContainer.Add(uri, new Cookie("os", "pc", "/", ".music.163.com"));
		cookieContainer.Add(uri, new Cookie("appver", "8.9.70", "/", ".music.163.com"));
		return cookieContainer;
	}

	public static void ResetCookies()
	{
		lock (CookieLock)
		{
			_cookies = NewContainer();
		}
	}

	public static void RestoreCookies(IDictionary<string, string> cookies)
	{
		lock (CookieLock)
		{
			_cookies = NewContainer();
			Uri uri = new Uri("https://music.163.com");
			int num = 0;
			foreach (KeyValuePair<string, string> cookie in cookies)
			{
				if (!string.IsNullOrEmpty(cookie.Key) && !string.IsNullOrEmpty(cookie.Value))
				{
					try
					{
						_cookies.Add(uri, new Cookie(cookie.Key, cookie.Value, "/", ".music.163.com"));
					}
					catch
					{
						num++;
					}
				}
			}
			if (num > 0)
			{
				BridgeLog.Warn("恢复会话时有 " + num + " 个 Cookie 被拒绝，登录状态可能不完整。");
			}
		}
	}

	public static Dictionary<string, string> ExportSessionCookies()
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		lock (CookieLock)
		{
			foreach (Cookie cookie in _cookies.GetCookies(new Uri("https://music.163.com")))
			{
				if (cookie.Name == "MUSIC_U" || cookie.Name == "__csrf" || cookie.Name == "NMTID" || cookie.Name == "MUSIC_A_T")
				{
					dictionary[cookie.Name] = cookie.Value;
				}
			}
			return dictionary;
		}
	}

	public static bool HasIdentityCookie()
	{
		lock (CookieLock)
		{
			foreach (Cookie cookie in _cookies.GetCookies(new Uri("https://music.163.com")))
			{
				if (cookie.Name == "MUSIC_U" && !string.IsNullOrEmpty(cookie.Value))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static string GetCookieValue(string name)
	{
		lock (CookieLock)
		{
			foreach (Cookie cookie in _cookies.GetCookies(new Uri("https://music.163.com")))
			{
				if (cookie.Name == name)
				{
					return cookie.Value;
				}
			}
		}
		return string.Empty;
	}

	public static string RequestUniKey(out bool networkError)
	{
		networkError = false;
		JObject jObject = Post("/weapi/login/qrcode/unikey", "{\"type\":1,\"csrf_token\":\"\"}", out networkError);
		if (jObject == null)
		{
			return null;
		}
		int valueOrDefault = jObject.Value<int?>("code").GetValueOrDefault();
		string text = jObject.Value<string>("unikey");
		if (valueOrDefault != 200 || string.IsNullOrEmpty(text))
		{
			BridgeLog.Warn("申请二维码 key 被服务端拒绝，code=" + valueOrDefault + "。");
			return null;
		}
		BridgeLog.Info("二维码已创建（key 长度 " + text.Length + "，内容不记录）。");
		return text;
	}

	public static string BuildQrPayload(string unikey)
	{
		return "https://music.163.com/login?codekey=" + unikey;
	}

	public static QrStatus CheckQrStatus(string unikey)
	{
		string plainJson = "{\"key\":\"" + JsonEscape(unikey) + "\",\"type\":1,\"csrf_token\":\"\"}";
		bool networkError;
		JObject jObject = Post("/weapi/login/qrcode/client/login", plainJson, out networkError);
		if (jObject == null)
		{
			if (!networkError)
			{
				return QrStatus.ProtocolError;
			}
			return QrStatus.NetworkError;
		}
		int valueOrDefault = jObject.Value<int?>("code").GetValueOrDefault();
		switch (valueOrDefault)
		{
		case 800:
			return QrStatus.Expired;
		case 801:
			return QrStatus.WaitingScan;
		case 802:
			return QrStatus.ScannedWaitingConfirm;
		case 803:
			return QrStatus.Success;
		default:
			BridgeLog.Warn("二维码轮询返回未知 code=" + valueOrDefault + "。");
			return QrStatus.ProtocolError;
		}
	}

	public static AccountCheck GetAccount(out AccountInfo info)
	{
		info = null;
		string cookieValue = GetCookieValue("__csrf");
		string plainJson = "{\"csrf_token\":\"" + JsonEscape(cookieValue) + "\"}";
		bool networkError;
		JObject jObject = Post("/weapi/w/nuser/account/get", plainJson, out networkError, cookieValue);
		if (jObject == null)
		{
			if (!networkError)
			{
				return AccountCheck.ProtocolError;
			}
			return AccountCheck.NetworkError;
		}
		int valueOrDefault = jObject.Value<int?>("code").GetValueOrDefault();
		switch (valueOrDefault)
		{
		case 301:
			return AccountCheck.Unauthorized;
		default:
			BridgeLog.Warn("账号接口返回 code=" + valueOrDefault + "。");
			return AccountCheck.ProtocolError;
		case 200:
		{
			JToken jToken = jObject["profile"];
			if (jToken == null || jToken.Type == JTokenType.Null)
			{
				return AccountCheck.Unauthorized;
			}
			info = new AccountInfo
			{
				Nickname = (jToken.Value<string>("nickname") ?? ""),
				UserId = jToken.Value<long?>("userId").GetValueOrDefault()
			};
			return AccountCheck.Valid;
		}
		}
	}

	public static List<PlaylistInfo> GetUserPlaylists(long uid, out bool networkError)
	{
		networkError = false;
		List<PlaylistInfo> list = new List<PlaylistInfo>();
		HashSet<long> hashSet = new HashSet<long>();
		int num = 0;
		int userPlaylistPageSize = MusicBridgeOptions.Current.Netease.UserPlaylistPageSize;
		for (int i = 0; i < MusicBridgeOptions.Current.Netease.UserPlaylistMaximumPageCount; i++)
		{
			string plainJson = "{\"uid\":" + uid + ",\"limit\":" + userPlaylistPageSize + ",\"offset\":" + num + ",\"includeVideo\":true}";
			JObject jObject = Post("/weapi/user/playlist", plainJson, out networkError, GetCookieValue("__csrf"));
			if (jObject == null)
			{
				return null;
			}
			int valueOrDefault = jObject.Value<int?>("code").GetValueOrDefault();
			if (valueOrDefault != 200)
			{
				BridgeLog.Warn("用户歌单接口 code=" + valueOrDefault);
				return null;
			}
			if (!(jObject["playlist"] is JArray { Count: not 0 } jArray))
			{
				break;
			}
			foreach (JToken item in jArray)
			{
				PlaylistInfo playlistInfo = ParsePlaylist(item, uid);
				if (playlistInfo != null && hashSet.Add(playlistInfo.Id))
				{
					list.Add(playlistInfo);
				}
			}
			bool valueOrDefault2 = jObject.Value<bool?>("more") == true;
			num += jArray.Count;
			BridgeLog.Info("用户歌单第 " + (i + 1) + " 页：本页 " + jArray.Count + " 个，累计 " + list.Count + "，more=" + valueOrDefault2);
			if (!valueOrDefault2)
			{
				return list;
			}
		}
		BridgeLog.Warn("用户歌单分页达到 " + MusicBridgeOptions.Current.Netease.UserPlaylistMaximumPageCount + " 页安全上限且服务端仍报告 more=true，提交已取到的 " + list.Count + " 个歌单。");
		return list;
	}

	private static PlaylistInfo ParsePlaylist(JToken t, long myUid)
	{
		if (t == null)
		{
			return null;
		}
		long valueOrDefault = t.Value<long?>("id").GetValueOrDefault();
		if (valueOrDefault == 0L)
		{
			return null;
		}
		JToken jToken = t["creator"];
		long num = jToken?.Value<long?>("userId").GetValueOrDefault() ?? 0;
		return new PlaylistInfo
		{
			Id = valueOrDefault,
			Name = (t.Value<string>("name") ?? ""),
			CreatorName = ((jToken != null) ? (jToken.Value<string>("nickname") ?? "") : ""),
			CreatorUserId = num,
			TrackCount = t.Value<int?>("trackCount").GetValueOrDefault(),
			CoverUrl = ToHttps(t.Value<string>("coverImgUrl") ?? t.Value<string>("picUrl") ?? ""),
			IsMine = (myUid != 0L && num == myUid)
		};
	}

	public static List<long> GetLikedSongIds(long uid, out bool networkError)
	{
		JObject jObject = Post("/weapi/song/like/get", "{\"uid\":" + uid + "}", out networkError, GetCookieValue("__csrf"));
		if (jObject == null)
		{
			return null;
		}
		if (jObject.Value<int?>("code") != 200)
		{
			BridgeLog.Warn("喜欢列表 code=" + jObject.Value<int?>("code"));
			return null;
		}
		List<long> list = new List<long>();
		JArray jArray = jObject["ids"] as JArray;
		int num = 0;
		if (jArray != null)
		{
			foreach (JToken item in jArray)
			{
				long? num2 = item?.Value<long?>();
				if (num2.HasValue && num2.Value != 0L)
				{
					list.Add(num2.Value);
				}
				else
				{
					num++;
				}
			}
		}
		BridgeLog.Info("我喜欢的音乐：" + list.Count + " 首" + ((num > 0) ? ("（跳过 " + num + " 个无法解析的元素）") : "") + "。");
		return list;
	}

	public static List<long> GetPlaylistTrackIds(long playlistId, out bool networkError)
	{
		string plainJson = "{\"id\":" + playlistId + ",\"n\":100000,\"s\":0}";
		JObject jObject = Post("/weapi/v6/playlist/detail", plainJson, out networkError, GetCookieValue("__csrf"));
		if (jObject == null)
		{
			return null;
		}
		if (jObject.Value<int?>("code") != 200)
		{
			BridgeLog.Warn("歌单详情 code=" + jObject.Value<int?>("code"));
			return null;
		}
		JToken jToken = jObject["playlist"];
		if (jToken == null)
		{
			return null;
		}
		List<long> list = new List<long>();
		if (jToken["trackIds"] is JArray jArray)
		{
			foreach (JToken item in jArray)
			{
				long valueOrDefault = item.Value<long?>("id").GetValueOrDefault();
				if (valueOrDefault != 0L)
				{
					list.Add(valueOrDefault);
				}
			}
		}
		BridgeLog.Info("歌单 " + playlistId + " 的完整曲目 ID 数 = " + list.Count + "（trackCount=" + (jToken.Value<int?>("trackCount") ?? (-1)) + "）");
		return list;
	}

	public static List<long> GetAlbumTrackIds(long albumId, out bool networkError)
	{
		JObject jObject = Post("/weapi/v1/album/" + albumId, "{}", out networkError, GetCookieValue("__csrf"));
		if (jObject == null)
		{
			return null;
		}
		if (jObject.Value<int?>("code") != 200)
		{
			BridgeLog.Warn("专辑详情 code=" + jObject.Value<int?>("code"));
			return null;
		}
		List<long> list = new List<long>();
		if (jObject["songs"] is JArray jArray)
		{
			foreach (JToken item in jArray)
			{
				long valueOrDefault = item.Value<long?>("id").GetValueOrDefault();
				if (valueOrDefault != 0L)
				{
					list.Add(valueOrDefault);
				}
			}
		}
		return list;
	}

	public static List<TrackInfo> GetSongDetails(IList<long> ids, out bool networkError)
	{
		networkError = false;
		if (ids == null || ids.Count == 0)
		{
			return new List<TrackInfo>();
		}
		StringBuilder stringBuilder = new StringBuilder("[");
		for (int i = 0; i < ids.Count; i++)
		{
			if (i > 0)
			{
				stringBuilder.Append(',');
			}
			stringBuilder.Append("{\\\"id\\\":").Append(ids[i]).Append('}');
		}
		stringBuilder.Append(']');
		string plainJson = "{\"c\":\"" + stringBuilder?.ToString() + "\"}";
		JObject jObject = Post("/weapi/v3/song/detail", plainJson, out networkError, GetCookieValue("__csrf"));
		if (jObject == null)
		{
			return null;
		}
		if (jObject.Value<int?>("code") != 200)
		{
			BridgeLog.Warn("歌曲详情 code=" + jObject.Value<int?>("code"));
			return null;
		}
		Dictionary<long, JToken> dictionary = new Dictionary<long, JToken>();
		if (jObject["privileges"] is JArray jArray)
		{
			foreach (JToken item in jArray)
			{
				long valueOrDefault = item.Value<long?>("id").GetValueOrDefault();
				if (valueOrDefault != 0L)
				{
					dictionary[valueOrDefault] = item;
				}
			}
		}
		Dictionary<long, TrackInfo> dictionary2 = new Dictionary<long, TrackInfo>();
		if (jObject["songs"] is JArray jArray2)
		{
			foreach (JToken item2 in jArray2)
			{
				TrackInfo trackInfo = ParseTrack(item2);
				if (trackInfo != null)
				{
					if (dictionary.TryGetValue(trackInfo.Id, out var value))
					{
						ApplyPrivilege(trackInfo, value);
					}
					dictionary2[trackInfo.Id] = trackInfo;
				}
			}
		}
		List<TrackInfo> list = new List<TrackInfo>(ids.Count);
		foreach (long id in ids)
		{
			if (dictionary2.TryGetValue(id, out var value2))
			{
				list.Add(value2);
			}
		}
		return list;
	}

	private static TrackInfo ParseTrack(JToken s)
	{
		if (s == null)
		{
			return null;
		}
		long valueOrDefault = s.Value<long?>("id").GetValueOrDefault();
		if (valueOrDefault == 0L)
		{
			return null;
		}
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		string[] array;
		if ((s["ar"] ?? s["artists"]) is JArray jArray)
		{
			foreach (JToken item in jArray)
			{
				string text = item.Value<string>("name");
				if (!string.IsNullOrEmpty(text))
				{
					list.Add(text);
				}
				array = new string[4] { "alia", "alias", "tns", "trans" };
				foreach (string key in array)
				{
					if (!(item[key] is JArray jArray2))
					{
						string text2 = item.Value<string>(key);
						if (!string.IsNullOrEmpty(text2))
						{
							list2.Add(text2);
						}
						continue;
					}
					foreach (JToken item2 in jArray2)
					{
						string text3 = item2.Value<string>();
						if (!string.IsNullOrEmpty(text3))
						{
							list2.Add(text3);
						}
					}
				}
			}
		}
		List<string> list3 = new List<string>();
		array = new string[3] { "alia", "alias", "tns" };
		foreach (string key2 in array)
		{
			if (!(s[key2] is JArray jArray3))
			{
				continue;
			}
			foreach (JToken item3 in jArray3)
			{
				string text4 = item3.Value<string>();
				if (!string.IsNullOrEmpty(text4))
				{
					list3.Add(text4);
				}
			}
		}
		JToken jToken = s["al"] ?? s["album"];
		return new TrackInfo
		{
			Id = valueOrDefault,
			Name = (s.Value<string>("name") ?? ""),
			Alias = ((list3.Count > 0) ? string.Join(" / ", list3.ToArray()) : ""),
			Artists = ((list.Count > 0) ? string.Join("/", list.ToArray()) : ""),
			ArtistAlias = ((list2.Count > 0) ? string.Join(" / ", list2.ToArray()) : ""),
			Album = ((jToken != null) ? (jToken.Value<string>("name") ?? "") : ""),
			CoverUrl = ((jToken != null) ? ToHttps(jToken.Value<string>("picUrl") ?? "") : ""),
			DurationMs = (s.Value<int?>("dt") ?? s.Value<int?>("duration").GetValueOrDefault())
		};
	}

	private static void ApplyPrivilege(TrackInfo t, JToken pv)
	{
		int valueOrDefault = pv.Value<int?>("st").GetValueOrDefault();
		int num = pv.Value<int?>("pl") ?? (-1);
		int valueOrDefault2 = pv.Value<int?>("fee").GetValueOrDefault();
		if (valueOrDefault < 0)
		{
			t.Playable = false;
			t.UnplayableReason = "无版权";
		}
		else if (num == 0)
		{
			t.Playable = false;
			t.UnplayableReason = valueOrDefault2 switch
			{
				4 => "需购买专辑",
				1 => "需要 VIP",
				_ => "当前账号不可播放",
			};
		}
		else
		{
			t.Playable = true;
			t.UnplayableReason = null;
		}
	}

	public static string GetSongUrl(long songId, out string failReason, out bool networkError, NeteaseRequestCancellation cancellation = null)
	{
		failReason = null;
		string plainJson = "{\"ids\":\"[" + songId + "]\",\"br\":128000}";
		JObject jObject = Post("/weapi/song/enhance/player/url", plainJson, out networkError, GetCookieValue("__csrf"), cancellation);
		if (jObject == null)
		{
			failReason = (networkError ? "网络错误" : "接口异常");
			return null;
		}
		if (jObject.Value<int?>("code") != 200)
		{
			failReason = "接口返回 code=" + jObject.Value<int?>("code");
			return null;
		}
		if (!(jObject["data"] is JArray { Count: not 0 } jArray))
		{
			failReason = "服务端未返回播放数据";
			return null;
		}
		JToken jToken = jArray[0];
		string text = jToken.Value<string>("url");
		int valueOrDefault = jToken.Value<int?>("code").GetValueOrDefault();
		int valueOrDefault2 = jToken.Value<int?>("fee").GetValueOrDefault();
		if (string.IsNullOrEmpty(text))
		{
			if (valueOrDefault == 404)
			{
				failReason = "该歌曲无版权";
			}
			else
			{
				switch (valueOrDefault2)
				{
				case 1:
					failReason = "该歌曲需要 VIP";
					break;
				case 4:
					failReason = "该歌曲需购买专辑";
					break;
				default:
					failReason = "当前账号无法播放该歌曲（code=" + valueOrDefault + "）";
					break;
				}
			}
			BridgeLog.Info("歌曲 " + songId + " 不可播放：" + failReason);
			return null;
		}
		text = ToHttps(text);
		string text2 = jToken.Value<string>("type") ?? "?";
		int valueOrDefault3 = jToken.Value<int?>("br").GetValueOrDefault();
		long valueOrDefault4 = jToken.Value<long?>("size").GetValueOrDefault();
		string text3 = "?";
		try
		{
			text3 = new Uri(text).Host;
		}
		catch
		{
		}
		BridgeLog.Info("取得播放地址：songId=" + songId + " 格式=" + text2 + " 码率=" + valueOrDefault3 + " 大小=" + valueOrDefault4 + " 主机=" + text3 + "（完整 URL 不记录）");
		return text;
	}

	public static bool GetLyrics(long songId, out string lrc, out string translated, out bool isPureMusic, out bool networkError)
	{
		lrc = null;
		translated = null;
		isPureMusic = false;
		string plainJson = "{\"id\":" + songId + ",\"lv\":-1,\"kv\":-1,\"tv\":-1}";
		JObject jObject = Post("/weapi/song/lyric", plainJson, out networkError, GetCookieValue("__csrf"));
		if (jObject == null)
		{
			return false;
		}
		if (jObject.Value<int?>("code") != 200)
		{
			return false;
		}
		isPureMusic = jObject.Value<bool?>("pureMusic") == true || jObject.Value<int?>("nolyric") == 1;
		JToken jToken = jObject["lrc"];
		if (jToken != null)
		{
			lrc = jToken.Value<string>("lyric");
		}
		JToken jToken2 = jObject["tlyric"];
		if (jToken2 != null)
		{
			translated = jToken2.Value<string>("lyric");
		}
		BridgeLog.Info("歌词 songId=" + songId + " 纯音乐=" + isPureMusic + " 原文长度=" + ((lrc != null) ? lrc.Length : 0) + " 翻译长度=" + ((translated != null) ? translated.Length : 0));
		return true;
	}

	public static bool SearchArtists(string keyword, int limit, out List<ArtistNameInfo> artists, out NeteaseSearchStatus status)
	{
		artists = null;
		JObject jObject = SearchRequest(BuildSearchBody(keyword, NeteaseSearchType.Artist, limit, 0), out status);
		if (jObject == null)
		{
			return false;
		}
		JToken? jToken = jObject["result"];
		artists = new List<ArtistNameInfo>();
		if (!(jToken["artists"] is JArray jArray))
		{
			return true;
		}
		foreach (JToken item in jArray)
		{
			ArtistNameInfo artistNameInfo = new ArtistNameInfo
			{
				Id = item.Value<long?>("id").GetValueOrDefault(),
				Name = (item.Value<string>("name") ?? ""),
				Trans = (item.Value<string>("trans") ?? "")
			};
			if (item["alias"] is JArray jArray2)
			{
				List<string> list = new List<string>();
				foreach (JToken item2 in jArray2)
				{
					string text = item2.Value<string>();
					if (!string.IsNullOrEmpty(text))
					{
						list.Add(text);
					}
				}
				artistNameInfo.Alias = string.Join(" / ", list.ToArray());
			}
			if (artistNameInfo.Name.Length > 0)
			{
				artists.Add(artistNameInfo);
			}
		}
		BridgeLog.Info("搜索歌手返回 " + artists.Count + " 条。");
		return true;
	}

	public static bool Search(string keyword, NeteaseSearchType type, int limit, int offset, out List<TrackInfo> songs, out List<PlaylistInfo> playlists, out NeteaseSearchStatus status)
	{
		songs = null;
		playlists = null;
		JObject jObject = SearchRequest(BuildSearchBody(keyword, type, limit, offset), out status);
		if (jObject == null)
		{
			return false;
		}
		JToken jToken = jObject["result"];
		switch (type)
		{
		case NeteaseSearchType.Song:
			songs = new List<TrackInfo>();
			if (jToken["songs"] is JArray jArray2)
			{
				foreach (JToken item in jArray2)
				{
					TrackInfo trackInfo = ParseTrack(item);
					if (trackInfo != null)
					{
						songs.Add(trackInfo);
					}
				}
			}
			BridgeLog.Info("搜索单曲返回 " + songs.Count + " 条。");
			break;
		case NeteaseSearchType.Playlist:
			playlists = new List<PlaylistInfo>();
			if (jToken["playlists"] is JArray jArray3)
			{
				foreach (JToken item2 in jArray3)
				{
					PlaylistInfo playlistInfo = ParsePlaylist(item2, 0L);
					if (playlistInfo != null)
					{
						playlists.Add(playlistInfo);
					}
				}
			}
			BridgeLog.Info("搜索歌单返回 " + playlists.Count + " 条。");
			break;
		case NeteaseSearchType.Album:
			playlists = new List<PlaylistInfo>();
			if (jToken["albums"] is JArray jArray)
			{
				foreach (JToken item3 in jArray)
				{
					long valueOrDefault = item3.Value<long?>("id").GetValueOrDefault();
					if (valueOrDefault != 0L)
					{
						JToken jToken2 = item3["artist"];
						playlists.Add(new PlaylistInfo
						{
							Id = valueOrDefault,
							Name = (item3.Value<string>("name") ?? ""),
							CreatorName = ((jToken2 != null) ? (jToken2.Value<string>("name") ?? "") : ""),
							TrackCount = item3.Value<int?>("size").GetValueOrDefault(),
							CoverUrl = ToHttps(item3.Value<string>("picUrl") ?? ""),
							IsAlbum = true,
							AlbumType = ComposeAlbumType(item3)
						});
					}
				}
			}
			BridgeLog.Info("搜索专辑返回 " + playlists.Count + " 条。");
			break;
		}
		return true;
	}

	private static string ComposeAlbumType(JToken album)
	{
		if (album == null)
		{
			return "";
		}
		string text = (album.Value<string>("type") ?? "").Trim();
		string text2 = (album.Value<string>("subType") ?? "").Trim();
		if (text.Length == 0)
		{
			return text2;
		}
		if (text2.Length == 0 || string.Equals(text2, text, StringComparison.OrdinalIgnoreCase))
		{
			return text;
		}
		return text + " · " + text2;
	}

	private static string BuildSearchBody(string keyword, NeteaseSearchType type, int limit, int offset)
	{
		string[] obj = new string[9]
		{
			"{\"s\":\"",
			JsonEscape(keyword),
			"\",\"type\":",
			null,
			null,
			null,
			null,
			null,
			null
		};
		int num = (int)type;
		obj[3] = num.ToString();
		obj[4] = ",\"limit\":";
		obj[5] = limit.ToString();
		obj[6] = ",\"offset\":";
		obj[7] = offset.ToString();
		obj[8] = ",\"total\":true}";
		return string.Concat(obj);
	}

	private static JObject SearchRequest(string body, out NeteaseSearchStatus status)
	{
		JObject jObject = TrySearchRoute("/weapi/cloudsearch/pc", body, out var status2);
		if (jObject != null)
		{
			status = NeteaseSearchStatus.Success;
			return jObject;
		}
		BridgeLog.Warn("搜索主端点不可用（" + status2.ToString() + "），尝试兼容端点。");
		jObject = TrySearchRoute("/weapi/search/get", body, out var status3);
		if (jObject != null)
		{
			status = NeteaseSearchStatus.Success;
			return jObject;
		}
		status = MergeSearchStatus(status2, status3);
		BridgeLog.Warn("搜索两个端点均失败，最终状态=" + status.ToString() + "。");
		return null;
	}

	private static JObject TrySearchRoute(string path, string body, out NeteaseSearchStatus status)
	{
		bool networkError;
		JObject jObject = Post(path, body, out networkError, GetCookieValue("__csrf"));
		if (jObject == null)
		{
			status = (networkError ? NeteaseSearchStatus.NetworkError : NeteaseSearchStatus.ProtocolError);
			return null;
		}
		int valueOrDefault = jObject.Value<int?>("code").GetValueOrDefault();
		if (valueOrDefault != 200)
		{
			BridgeLog.Warn("搜索端点 " + path + " 被服务端拒绝，code=" + valueOrDefault + "。");
			status = NeteaseSearchStatus.ServiceRejected;
			return null;
		}
		if (!(jObject["result"] is JObject))
		{
			BridgeLog.Warn("搜索端点 " + path + " 返回的 result 结构无效。");
			status = NeteaseSearchStatus.ProtocolError;
			return null;
		}
		status = NeteaseSearchStatus.Success;
		return jObject;
	}

	private static NeteaseSearchStatus MergeSearchStatus(NeteaseSearchStatus first, NeteaseSearchStatus second)
	{
		if (first == NeteaseSearchStatus.NetworkError || second == NeteaseSearchStatus.NetworkError)
		{
			return NeteaseSearchStatus.NetworkError;
		}
		if (first == NeteaseSearchStatus.ServiceRejected || second == NeteaseSearchStatus.ServiceRejected)
		{
			return NeteaseSearchStatus.ServiceRejected;
		}
		return NeteaseSearchStatus.ProtocolError;
	}

	public static List<PlaylistInfo> GetDailyPlaylists(out bool networkError, out string error)
	{
		error = null;
		JObject jObject = Post("/weapi/v1/discovery/recommend/resource", "{}", out networkError, GetCookieValue("__csrf"));
		if (jObject == null)
		{
			error = (networkError ? "网络错误" : "接口异常");
			return null;
		}
		int valueOrDefault = jObject.Value<int?>("code").GetValueOrDefault();
		if (valueOrDefault != 200)
		{
			error = "接口返回 code=" + valueOrDefault;
			return null;
		}
		List<PlaylistInfo> list = new List<PlaylistInfo>();
		if (jObject["recommend"] is JArray jArray)
		{
			foreach (JToken item in jArray)
			{
				long valueOrDefault2 = item.Value<long?>("id").GetValueOrDefault();
				if (valueOrDefault2 != 0L)
				{
					JToken jToken = item["creator"];
					list.Add(new PlaylistInfo
					{
						Id = valueOrDefault2,
						Name = (item.Value<string>("name") ?? ""),
						CreatorName = ((jToken != null) ? (jToken.Value<string>("nickname") ?? "") : ""),
						TrackCount = item.Value<int?>("trackCount").GetValueOrDefault(),
						CoverUrl = ToHttps(item.Value<string>("picUrl") ?? "")
					});
				}
			}
		}
		BridgeLog.Info("每日推荐歌单 " + list.Count + " 个。");
		return list;
	}

	public static List<TrackInfo> GetDailySongs(out bool networkError, out string error)
	{
		error = null;
		JObject jObject = Post("/weapi/v3/discovery/recommend/songs", "{}", out networkError, GetCookieValue("__csrf"));
		if (jObject == null)
		{
			error = (networkError ? "网络错误" : "接口异常");
			return null;
		}
		int valueOrDefault = jObject.Value<int?>("code").GetValueOrDefault();
		if (valueOrDefault != 200)
		{
			error = "接口返回 code=" + valueOrDefault;
			return null;
		}
		List<TrackInfo> list = new List<TrackInfo>();
		JToken jToken = jObject["data"];
		JArray jArray = ((jToken != null) ? (jToken["dailySongs"] as JArray) : null);
		if (jArray != null)
		{
			foreach (JToken item in jArray)
			{
				TrackInfo trackInfo = ParseTrack(item);
				if (trackInfo != null)
				{
					JToken jToken2 = item["privilege"];
					if (jToken2 != null)
					{
						ApplyPrivilege(trackInfo, jToken2);
					}
					list.Add(trackInfo);
				}
			}
		}
		BridgeLog.Info("每日推荐歌曲 " + list.Count + " 首。");
		return list;
	}

	private static JObject Post(string path, string plainJson, out bool networkError, string csrf = null, NeteaseRequestCancellation cancellation = null)
	{
		networkError = false;
		HttpWebRequest httpWebRequest = null;
		try
		{
			if (cancellation != null && cancellation.IsCancelled)
			{
				return null;
			}
			NeteaseCrypto.Encrypt(plainJson, out var paramsValue, out var encSecKey);
			string text = "https://music.163.com" + path;
			if (!string.IsNullOrEmpty(csrf))
			{
				text = text + "?csrf_token=" + Uri.EscapeDataString(csrf);
			}
			httpWebRequest = (HttpWebRequest)WebRequest.Create(text);
			cancellation?.Attach(httpWebRequest);
			httpWebRequest.Method = "POST";
			httpWebRequest.ContentType = "application/x-www-form-urlencoded";
			httpWebRequest.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
			httpWebRequest.Referer = "https://music.163.com/";
			httpWebRequest.Headers["Origin"] = "https://music.163.com";
			httpWebRequest.Accept = "*/*";
			httpWebRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
			int readWriteTimeout = (httpWebRequest.Timeout = (int)MusicBridgeOptions.Current.Shared.HttpTimeout.TotalMilliseconds);
			httpWebRequest.ReadWriteTimeout = readWriteTimeout;
			httpWebRequest.KeepAlive = true;
			lock (CookieLock)
			{
				httpWebRequest.CookieContainer = _cookies;
			}
			string s = "params=" + Uri.EscapeDataString(paramsValue) + "&encSecKey=" + Uri.EscapeDataString(encSecKey);
			byte[] bytes = Encoding.UTF8.GetBytes(s);
			httpWebRequest.ContentLength = bytes.Length;
			using (Stream stream = httpWebRequest.GetRequestStream())
			{
				stream.Write(bytes, 0, bytes.Length);
			}
			using HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
			using StreamReader streamReader = new StreamReader(httpWebResponse.GetResponseStream(), Encoding.UTF8);
			string text2 = streamReader.ReadToEnd();
			BridgeLog.History("请求 " + path + " -> HTTP " + (int)httpWebResponse.StatusCode + "，响应 " + text2.Length + " 字节。");
			return JObject.Parse(text2);
		}
		catch (WebException ex)
		{
			if (ex.Response != null)
			{
				try
				{
					ex.Response.Close();
				}
				catch
				{
				}
			}
			networkError = ex.Status != WebExceptionStatus.ProtocolError;
			BridgeLog.Warn("请求 " + path + " 网络异常：" + ex.Status);
			return null;
		}
		catch (Exception ex2)
		{
			BridgeLog.Warn("请求 " + path + " 失败：" + ex2.GetType().Name);
			return null;
		}
		finally
		{
			if (cancellation != null && httpWebRequest != null)
			{
				cancellation.Detach(httpWebRequest);
			}
		}
	}

	public static string ToHttps(string url)
	{
		if (string.IsNullOrEmpty(url))
		{
			return url;
		}
		if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
		{
			return "https://" + url.Substring("http://".Length);
		}
		return url;
	}

	private static string JsonEscape(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return string.Empty;
		}
		StringBuilder stringBuilder = new StringBuilder(value.Length + 8);
		foreach (char c in value)
		{
			switch (c)
			{
			case '"':
				stringBuilder.Append("\\\"");
				continue;
			case '\\':
				stringBuilder.Append("\\\\");
				continue;
			case '\n':
				stringBuilder.Append("\\n");
				continue;
			case '\r':
				stringBuilder.Append("\\r");
				continue;
			case '\t':
				stringBuilder.Append("\\t");
				continue;
			}
			if (c < ' ')
			{
				StringBuilder stringBuilder2 = stringBuilder.Append("\\u");
				int num = c;
				stringBuilder2.Append(num.ToString("x4"));
			}
			else
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString();
	}
}
