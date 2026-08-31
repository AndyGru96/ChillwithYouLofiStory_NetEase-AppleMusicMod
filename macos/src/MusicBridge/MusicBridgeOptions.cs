using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MusicBridge;

internal sealed class MusicBridgeOptions
{
	public const int CurrentSchemaVersion = 1;

	public int SchemaVersion = 1;

	public SharedOptions Shared = new SharedOptions();

	public NeteaseOptions Netease = new NeteaseOptions();

	public AppleOptions Apple = new AppleOptions();

	public LyricsOptions Lyrics = new LyricsOptions();

	public UiOptions UI = new UiOptions();

	public LocalOptions Local = new LocalOptions();

	public DebugOptions Debug = new DebugOptions();

	public static MusicBridgeOptions Current { get; private set; } = new MusicBridgeOptions();

	public static string Source { get; private set; } = "内建默认值";

	public static void Load()
	{
		MusicBridgeOptions musicBridgeOptions = new MusicBridgeOptions();
		string text = BridgePaths.Resolve("config", "musicbridge.options.json");
		try
		{
			if (File.Exists(text))
			{
				string text2 = File.ReadAllText(text);
				JObject jObject = JObject.Parse(text2);
				List<string> list = new List<string>();
				FindUnknown(jObject, typeof(MusicBridgeOptions), "", list);
				if (list.Count > 0)
				{
					throw new InvalidDataException("未知参数：" + string.Join(", ", list.ToArray()));
				}
				int valueOrDefault = jObject.Value<int?>("SchemaVersion").GetValueOrDefault();
				if (valueOrDefault != 1)
				{
					throw new InvalidDataException("SchemaVersion=" + valueOrDefault + "，当前只接受 " + 1);
				}
				JsonConvert.PopulateObject(text2, musicBridgeOptions);
				Source = text;
			}
			Validate(musicBridgeOptions);
			Current = musicBridgeOptions;
		}
		catch (Exception ex)
		{
			Current = new MusicBridgeOptions();
			Source = "内建默认值（配置被拒绝：" + ex.Message + "）";
			BridgeLog.Warn("配置未加载，安全回退到唯一默认值：" + ex.Message);
		}
		Report();
	}

	private static void Validate(MusicBridgeOptions o)
	{
		RequireAllTimeSpans(o.Shared, "Shared");
		RequireAllTimeSpans(o.Netease, "Netease");
		RequireAllTimeSpans(o.Apple, "Apple");
		RequireAllTimeSpans(o.Lyrics, "Lyrics");
		Require(o.Shared.HttpTimeout, TimeSpan.FromSeconds(1.0), TimeSpan.FromMinutes(2.0), "Shared.HttpTimeout");
		Require(o.Shared.CoverMaximumConcurrentDownloads, 1, 32, "Shared.CoverMaximumConcurrentDownloads");
		Require(o.Shared.CoverMaximumEntries, 1, 100000, "Shared.CoverMaximumEntries");
		Require(o.Netease.QrPollInterval, TimeSpan.FromMilliseconds(500.0), TimeSpan.FromSeconds(10.0), "Netease.QrPollInterval");
		Require(o.Netease.QrLifetime, TimeSpan.FromSeconds(30.0), TimeSpan.FromMinutes(15.0), "Netease.QrLifetime");
		Require(o.Netease.SongDetailBatchSize, 1, 500, "Netease.SongDetailBatchSize");
		Require(o.Netease.UserPlaylistPageSize, 1, 1000, "Netease.UserPlaylistPageSize");
		Require(o.Netease.UserPlaylistMaximumPageCount, 1, 1000, "Netease.UserPlaylistMaximumPageCount");
		Require(o.Netease.ServicePointConnectionLimit, 1, 1024, "Netease.ServicePointConnectionLimit");
		Require(o.Netease.SearchPageSize, 1, 100, "Netease.SearchPageSize");
		Require(o.Netease.AudioCacheCapacityBytes, 0L, 21474836480L, "Netease.AudioCacheCapacityBytes");
		Require(o.Netease.AudioCacheMaximumFileBytes, 1024L, 1073741824L, "Netease.AudioCacheMaximumFileBytes");
		Require(o.Netease.SessionMaximumFileBytes, 1024L, 10485760L, "Netease.SessionMaximumFileBytes");
		Require(o.Shared.CoverMaximumDecodedBytes, 1048576L, 2147483648L, "Shared.CoverMaximumDecodedBytes");
		Require(o.Shared.LogMaximumFileBytes, 65536L, 1073741824L, "Shared.LogMaximumFileBytes");
		Require(o.Shared.LogRetainDays, 1, 365, "Shared.LogRetainDays");
		Require(o.Apple.ItemContainerMaximumItems, 100, 100000, "Apple.ItemContainerMaximumItems");
		Require(o.Apple.PendingCacheMaximumAge, TimeSpan.FromMinutes(1.0), TimeSpan.FromDays(2.0), "Apple.PendingCacheMaximumAge");
		Require(o.Apple.KeyChordStepDelay, TimeSpan.Zero, TimeSpan.FromSeconds(2.0), "Apple.KeyChordStepDelay");
		Require(o.Apple.KeyChordHoldDelay, TimeSpan.Zero, TimeSpan.FromSeconds(2.0), "Apple.KeyChordHoldDelay");
		Require(o.Apple.PaneOpenSettleDelay, TimeSpan.Zero, TimeSpan.FromSeconds(10.0), "Apple.PaneOpenSettleDelay");
		Require(o.Apple.EmptyLibraryRetryDelay, TimeSpan.FromMilliseconds(100.0), TimeSpan.FromMinutes(1.0), "Apple.EmptyLibraryRetryDelay");
		Require(o.Apple.PageReadyPollInterval, TimeSpan.FromMilliseconds(10.0), TimeSpan.FromSeconds(5.0), "Apple.PageReadyPollInterval");
		Require(o.Apple.EmptyLibraryMaximumRetryCount, 0, 20, "Apple.EmptyLibraryMaximumRetryCount");
		Require(o.Apple.StabilityVerificationCount, 1, 10, "Apple.StabilityVerificationCount");
		Require(o.Apple.PageReadyMaximumPollCount, 1, 200, "Apple.PageReadyMaximumPollCount");
		Require(o.Lyrics.MaximumCrossLanguageQueries, 0, 20, "Lyrics.MaximumCrossLanguageQueries");
		Require(o.Lyrics.ITunesRequestMinimumInterval, TimeSpan.Zero, TimeSpan.FromMinutes(1.0), "Lyrics.ITunesRequestMinimumInterval");
		Require(o.Lyrics.ITunesRetryBaseDelay, TimeSpan.Zero, TimeSpan.FromMinutes(1.0), "Lyrics.ITunesRetryBaseDelay");
		Require(o.Lyrics.ITunesRequestTimeout, TimeSpan.FromSeconds(1.0), TimeSpan.FromMinutes(2.0), "Lyrics.ITunesRequestTimeout");
		Require(o.Lyrics.ITunesMaximumRetryCount, 0, 5, "Lyrics.ITunesMaximumRetryCount");
		Require(o.Lyrics.ITunesPersistentCacheMaximumEntries, 10, 100000, "Lyrics.ITunesPersistentCacheMaximumEntries");
		Require(o.UI.PlaylistRowHeightPixels, 20f, 200f, "UI.PlaylistRowHeightPixels");
		Require(o.UI.TrackRowHeightPixels, 20f, 200f, "UI.TrackRowHeightPixels");
		Require(o.UI.FolderIndentPixels, 0f, 200f, "UI.FolderIndentPixels");
		Require(o.UI.RenderPageSize, 10, 1000, "UI.RenderPageSize");
		Require(o.UI.SearchDebounceSeconds, 0f, 10f, "UI.SearchDebounceSeconds");
		Require(o.Lyrics.PositionQuantizationCenterSeconds, 0.0, 1.0, "Lyrics.PositionQuantizationCenterSeconds");
		Require(o.Local.VirtualizeThreshold, 20, 100000, "Local.VirtualizeThreshold");
		Require(o.Local.LoadedClipBudget, 2, 1000, "Local.LoadedClipBudget");
		Require(o.Local.LoadedClipBudgetMegabytes, 16, 65536, "Local.LoadedClipBudgetMegabytes");
		if (o.Local.UnlimitedImport && !o.Local.VirtualizeNativeList)
		{
			BridgeLog.Warn("配置组合有风险：Local.UnlimitedImport 已开但 Local.VirtualizeNativeList 关着。曲目超过 " + o.Local.VirtualizeThreshold + " 首后播放列表会严重卡顿甚至卡死，建议一并开启。");
		}
	}

	private static void RequireAllTimeSpans(object group, string groupName)
	{
		FieldInfo[] fields = group.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
		foreach (FieldInfo fieldInfo in fields)
		{
			if (!(fieldInfo.FieldType != typeof(TimeSpan)))
			{
				Require((TimeSpan)fieldInfo.GetValue(group), TimeSpan.Zero, TimeSpan.FromDays(1.0), groupName + "." + fieldInfo.Name);
			}
		}
	}

	private static void Require(TimeSpan value, TimeSpan min, TimeSpan max, string name)
	{
		if (value < min || value > max)
		{
			throw new InvalidDataException(name + " 越界：" + value);
		}
	}

	private static void Require(int value, int min, int max, string name)
	{
		if (value < min || value > max)
		{
			throw new InvalidDataException(name + " 越界：" + value);
		}
	}

	private static void Require(long value, long min, long max, string name)
	{
		if (value < min || value > max)
		{
			throw new InvalidDataException(name + " 越界：" + value);
		}
	}

	private static void Require(float value, float min, float max, string name)
	{
		if (float.IsNaN(value) || value < min || value > max)
		{
			throw new InvalidDataException(name + " 越界：" + value);
		}
	}

	private static void Require(double value, double min, double max, string name)
	{
		if (double.IsNaN(value) || double.IsInfinity(value) || value < min || value > max)
		{
			throw new InvalidDataException(name + " 越界：" + value);
		}
	}

	private static void FindUnknown(JObject json, Type type, string prefix, List<string> unknown)
	{
		Dictionary<string, FieldInfo> dictionary = new Dictionary<string, FieldInfo>(StringComparer.OrdinalIgnoreCase);
		FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
		foreach (FieldInfo fieldInfo in fields)
		{
			dictionary[fieldInfo.Name] = fieldInfo;
		}
		foreach (JProperty item in json.Properties())
		{
			string text = ((prefix.Length == 0) ? item.Name : (prefix + "." + item.Name));
			if (!dictionary.TryGetValue(item.Name, out var value))
			{
				unknown.Add(text);
			}
			else if (item.Value is JObject json2)
			{
				FindUnknown(json2, value.FieldType, text, unknown);
			}
		}
	}

	private static void Report()
	{
		MusicBridgeOptions current = Current;
		BridgeLog.Info("配置最终来源=" + Source + "；SchemaVersion=" + current.SchemaVersion + "；HTTP超时=" + current.Shared.HttpTimeout.TotalSeconds + "s；网易云轮询=" + current.Netease.QrPollInterval.TotalMilliseconds + "ms；音频缓存=" + current.Netease.AudioCacheCapacityBytes + "B；Apple枚举上限=" + current.Apple.ItemContainerMaximumItems + "；iTunes最小请求间隔=" + current.Lyrics.ITunesRequestMinimumInterval.TotalMilliseconds + "ms；调试快捷键=" + current.Debug.EnableHotkeys + "；AM自动同步标记=" + current.Debug.EnableAppleAutoSyncFlag + "。");
	}
}
