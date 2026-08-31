using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MusicBridge;

internal static class ITunesNameHub
{
	private sealed class PersistentEntry
	{
		public string Key;

		public DateTime LastAccessUtc;

		public AmNameSet Value;
	}

	private sealed class PersistentFile
	{
		public int Version = 1;

		public List<PersistentEntry> Entries = new List<PersistentEntry>();
	}

	private static readonly string[] SearchStores = new string[5] { "us", "cn", "jp", "tw", "hk" };

	private static readonly string[] LookupStores = new string[4] { "cn", "jp", "hk", "us" };

	private static readonly Dictionary<string, AmNameSet> Cache = new Dictionary<string, AmNameSet>(StringComparer.Ordinal);

	private static readonly object Gate = new object();

	private static readonly object RequestGate = new object();

	private static DateTime NextRequestUtc = DateTime.MinValue;

	private static bool PersistentCacheLoaded;

	public static string Storefront { get; private set; }

	public static string StorefrontLabel
	{
		get
		{
			if (Storefront == null)
			{
				return null;
			}
			switch (Storefront)
			{
			case "cn":
				return "简体中文地区";
			case "tw":
			case "hk":
				return "繁体中文地区";
			case "jp":
				return "日语地区";
			case "kr":
				return "韩语地区";
			case "us":
			case "gb":
				return "英语地区";
			default:
				return Storefront.ToUpperInvariant() + " 地区";
			}
		}
	}

	private static string PersistentCachePath => BridgePaths.Resolve("cache", "itunes_names.json");

	public static AmNameSet Resolve(string title, string artist, double seconds, out bool transientError)
	{
		transientError = false;
		if (string.IsNullOrEmpty(title))
		{
			return null;
		}
		if (seconds <= 0.0)
		{
			BridgeLog.Info("[名字] SMTC 没给时长，跳过 iTunes（没有可靠判据，宁可不查）。");
			return null;
		}
		string key = title + "\u001f" + artist + "\u001f" + (int)seconds;
		EnsurePersistentCacheLoaded();
		lock (Gate)
		{
			if (Cache.TryGetValue(key, out var value))
			{
				return value;
			}
		}
		AmNameSet amNameSet = null;
		try
		{
			amNameSet = ResolveCore(title, artist, seconds, out transientError);
		}
		catch (Exception ex)
		{
			transientError = true;
			BridgeLog.Warn("[名字] iTunes 查询失败：" + ex.Message);
		}
		if (amNameSet != null && !transientError)
		{
			lock (Gate)
			{
				Cache[key] = amNameSet;
			}
			SavePersistentCache();
		}
		return amNameSet;
	}

	private static AmNameSet ResolveCore(string title, string artist, double seconds, out bool transientError)
	{
		transientError = false;
		long num = 0L;
		foreach (string item in StoreOrder())
		{
			num = FindTrackId(title + " " + artist, item, seconds, out var requestFailed);
			if (requestFailed)
			{
				transientError = true;
			}
			if (num != 0L)
			{
				break;
			}
			if (!string.IsNullOrEmpty(artist))
			{
				num = FindTrackId(artist, item, seconds, out requestFailed);
				if (requestFailed)
				{
					transientError = true;
				}
				if (num != 0L)
				{
					break;
				}
			}
		}
		if (num == 0L)
		{
			return null;
		}
		AmNameSet amNameSet = new AmNameSet
		{
			TrackId = num,
			Seconds = seconds
		};
		AddDistinct(amNameSet.Titles, title);
		AddDistinct(amNameSet.Artists, artist);
		string[] lookupStores = LookupStores;
		foreach (string text in lookupStores)
		{
			bool requestFailed2;
			JObject json = GetJson("https://itunes.apple.com/lookup?id=" + num + "&country=" + text, out requestFailed2);
			if (requestFailed2)
			{
				transientError = true;
			}
			if (json != null && json["results"] is JArray { Count: not 0 } jArray)
			{
				JToken jToken = jArray[0];
				string text2 = jToken.Value<string>("trackName");
				string text3 = jToken.Value<string>("artistName");
				string text4 = jToken.Value<string>("collectionName");
				if (Storefront == null && !string.IsNullOrEmpty(text2) && string.Equals(text2.Trim(), (title ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
				{
					SetStorefront(text);
				}
				AddDistinct(amNameSet.Titles, text2);
				AddDistinct(amNameSet.Artists, text3);
				AddDistinct(amNameSet.Albums, text4);
				if (!string.IsNullOrWhiteSpace(text2))
				{
					amNameSet.LocalizedNames.Add(new AmLocalizedName
					{
						Storefront = text,
						Title = text2.Trim(),
						Artist = (text3 ?? "").Trim(),
						Album = (text4 ?? "").Trim()
					});
				}
			}
		}
		if (amNameSet.IsEmpty)
		{
			return null;
		}
		BridgeLog.History("[名字] 『" + title + "』-> " + amNameSet);
		BridgeLog.Info("[名字] iTunes 多区名称集合已建立（曲名=" + amNameSet.Titles.Count + "，歌手=" + amNameSet.Artists.Count + "，专辑=" + amNameSet.Albums.Count + "）。");
		return amNameSet;
	}

	private static IEnumerable<string> StoreOrder()
	{
		if (Storefront != null)
		{
			yield return Storefront;
		}
		string[] searchStores = SearchStores;
		foreach (string text in searchStores)
		{
			if (text != Storefront)
			{
				yield return text;
			}
		}
	}

	private static void SetStorefront(string cc)
	{
		Storefront = cc;
		BridgeLog.Info("[名字] 检测到 Apple Music 区服 = " + cc + "（" + StorefrontLabel + "），已记住。");
		try
		{
			AtomicFile.WriteAllText(Path.Combine(BridgePaths.Config, "applemusic_store.txt"), cc);
		}
		catch (Exception ex)
		{
			BridgeLog.Info("[名字] 区服写盘失败：" + ex.Message);
		}
	}

	public static void LoadStorefront()
	{
		EnsurePersistentCacheLoaded();
		try
		{
			string path = Path.Combine(BridgePaths.Config, "applemusic_store.txt");
			if (File.Exists(path))
			{
				string text = File.ReadAllText(path).Trim().ToLowerInvariant();
				if (text.Length == 2)
				{
					Storefront = text;
					BridgeLog.Info("[名字] 沿用已记住的区服 " + text);
				}
			}
		}
		catch
		{
		}
	}

	private static long FindTrackId(string term, string country, double seconds, out bool requestFailed)
	{
		requestFailed = false;
		if (string.IsNullOrWhiteSpace(term))
		{
			return 0L;
		}
		JObject json = GetJson("https://itunes.apple.com/search?entity=song&limit=25&country=" + country + "&term=" + Uri.EscapeDataString(term.Trim()), out requestFailed);
		if (json == null)
		{
			return 0L;
		}
		if (!(json["results"] is JArray jArray))
		{
			return 0L;
		}
		long result = 0L;
		double num = double.MaxValue;
		foreach (JToken item in jArray)
		{
			long valueOrDefault = item.Value<long?>("trackId").GetValueOrDefault();
			int valueOrDefault2 = item.Value<int?>("trackTimeMillis").GetValueOrDefault();
			if (valueOrDefault != 0L && valueOrDefault2 > 0)
			{
				if (seconds <= 0.0)
				{
					return valueOrDefault;
				}
				double num2 = Math.Abs((double)valueOrDefault2 / 1000.0 - seconds);
				if (num2 <= 2.0 && num2 < num)
				{
					num = num2;
					result = valueOrDefault;
				}
			}
		}
		return result;
	}

	private static void AddDistinct(List<string> list, string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return;
		}
		string text = value.Trim();
		foreach (string item in list)
		{
			if (string.Equals(item, text, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
		}
		list.Add(text);
	}

	private static JObject GetJson(string url, out bool requestFailed)
	{
		requestFailed = false;
		int iTunesMaximumRetryCount = MusicBridgeOptions.Current.Lyrics.ITunesMaximumRetryCount;
		for (int i = 0; i <= iTunesMaximumRetryCount; i++)
		{
			WaitForRequestSlot();
			try
			{
				HttpWebRequest obj = (HttpWebRequest)WebRequest.Create(url);
				obj.Method = "GET";
				int readWriteTimeout = (obj.Timeout = (int)MusicBridgeOptions.Current.Lyrics.ITunesRequestTimeout.TotalMilliseconds);
				obj.ReadWriteTimeout = readWriteTimeout;
				obj.UserAgent = "ChillWithYouMusicBridge/1.2.0";
				obj.CookieContainer = null;
				obj.UseDefaultCredentials = false;
				using HttpWebResponse httpWebResponse = (HttpWebResponse)obj.GetResponse();
				using StreamReader streamReader = new StreamReader(httpWebResponse.GetResponseStream(), Encoding.UTF8);
				if (httpWebResponse.StatusCode != HttpStatusCode.OK)
				{
					throw new WebException("HTTP " + (int)httpWebResponse.StatusCode);
				}
				return JObject.Parse(streamReader.ReadToEnd());
			}
			catch (Exception ex)
			{
				if (i >= iTunesMaximumRetryCount)
				{
					requestFailed = true;
					BridgeLog.Info("[名字] 请求失败：" + ex.Message);
					return null;
				}
				double num2 = Math.Pow(2.0, i);
				int num3 = (int)(MusicBridgeOptions.Current.Lyrics.ITunesRetryBaseDelay.TotalMilliseconds * num2);
				if (num3 > 0)
				{
					Thread.Sleep(num3);
				}
			}
		}
		requestFailed = true;
		return null;
	}

	private static void WaitForRequestSlot()
	{
		lock (RequestGate)
		{
			DateTime utcNow = DateTime.UtcNow;
			if (NextRequestUtc > utcNow)
			{
				int num = (int)Math.Ceiling((NextRequestUtc - utcNow).TotalMilliseconds);
				if (num > 0)
				{
					Thread.Sleep(num);
				}
			}
			NextRequestUtc = DateTime.UtcNow + MusicBridgeOptions.Current.Lyrics.ITunesRequestMinimumInterval;
		}
	}

	private static void EnsurePersistentCacheLoaded()
	{
		lock (Gate)
		{
			if (PersistentCacheLoaded)
			{
				return;
			}
			PersistentCacheLoaded = true;
			try
			{
				string persistentCachePath = PersistentCachePath;
				if (!File.Exists(persistentCachePath))
				{
					return;
				}
				PersistentFile persistentFile = JsonConvert.DeserializeObject<PersistentFile>(File.ReadAllText(persistentCachePath));
				if (persistentFile == null || persistentFile.Version != 1 || persistentFile.Entries == null)
				{
					return;
				}
				foreach (PersistentEntry entry in persistentFile.Entries)
				{
					if (entry != null && !string.IsNullOrEmpty(entry.Key) && entry.Value != null && !entry.Value.IsEmpty)
					{
						Cache[entry.Key] = entry.Value;
					}
				}
				BridgeLog.Info("[名字] 已加载 iTunes 名称缓存 " + Cache.Count + " 项。");
			}
			catch (Exception ex)
			{
				BridgeLog.Warn("[名字] iTunes 名称缓存已忽略：" + ex.Message);
			}
		}
	}

	private static void SavePersistentCache()
	{
		try
		{
			PersistentFile persistentFile = new PersistentFile();
			lock (Gate)
			{
				int iTunesPersistentCacheMaximumEntries = MusicBridgeOptions.Current.Lyrics.ITunesPersistentCacheMaximumEntries;
				int num = Math.Max(0, Cache.Count - iTunesPersistentCacheMaximumEntries);
				int num2 = 0;
				foreach (KeyValuePair<string, AmNameSet> item in Cache)
				{
					if (num2++ >= num)
					{
						persistentFile.Entries.Add(new PersistentEntry
						{
							Key = item.Key,
							LastAccessUtc = DateTime.UtcNow,
							Value = item.Value
						});
					}
				}
			}
			AtomicFile.WriteAllText(PersistentCachePath, JsonConvert.SerializeObject(persistentFile, Formatting.None));
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("[名字] iTunes 名称缓存写入失败：" + ex.Message);
		}
	}
}
