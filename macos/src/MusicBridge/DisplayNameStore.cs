using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MusicBridge;

internal static class DisplayNameStore
{
	private const string FileName = "display_names.json";

	private static string _legacyNetease = "";

	private static readonly Dictionary<long, string> NeteaseByUserId = new Dictionary<long, string>();

	private static string _apple = "";

	private static bool _loaded;

	private static readonly object Gate = new object();

	private static string Path_ => Path.Combine(BridgePaths.Config, "display_names.json");

	public static string Apple
	{
		get
		{
			Load();
			return _apple;
		}
	}

	public static string GetNetease(long userId)
	{
		Load();
		if (userId > 0 && NeteaseByUserId.TryGetValue(userId, out var value))
		{
			return value;
		}
		return _legacyNetease;
	}

	public static void SetNetease(long userId, string name)
	{
		if (userId > 0 && !string.IsNullOrWhiteSpace(name))
		{
			Load();
			string text = name.Trim();
			if (!NeteaseByUserId.TryGetValue(userId, out var value) || !string.Equals(value, text, StringComparison.Ordinal))
			{
				NeteaseByUserId[userId] = text;
				Save();
				BridgeLog.Info("已按稳定 userId 记住网易云昵称。");
			}
		}
	}

	public static void SetApple(string name)
	{
		if (!string.IsNullOrWhiteSpace(name))
		{
			Load();
			if (!string.Equals(_apple, name, StringComparison.Ordinal))
			{
				_apple = name.Trim();
				Save();
				BridgeLog.Info("已记住 Apple Music 账号名（覆盖旧值）。");
			}
		}
	}

	private static void Load()
	{
		lock (Gate)
		{
			if (_loaded)
			{
				return;
			}
			_loaded = true;
			try
			{
				if (!File.Exists(Path_))
				{
					return;
				}
				JObject jObject = JObject.Parse(File.ReadAllText(Path_));
				_legacyNetease = jObject.Value<string>("netease") ?? "";
				if (jObject["neteaseByUserId"] is JObject jObject2)
				{
					foreach (JProperty item in jObject2.Properties())
					{
						string value = item.Value.Value<string>();
						if (long.TryParse(item.Name, out var result) && result > 0 && !string.IsNullOrWhiteSpace(value))
						{
							NeteaseByUserId[result] = value;
						}
					}
				}
				_apple = jObject.Value<string>("apple") ?? "";
			}
			catch (Exception ex)
			{
				BridgeLog.Warn("读取昵称存档失败：" + ex.Message);
			}
		}
	}

	private static void Save()
	{
		lock (Gate)
		{
			try
			{
				JObject jObject = new JObject();
				foreach (KeyValuePair<long, string> item in NeteaseByUserId)
				{
					jObject[item.Key.ToString()] = item.Value;
				}
				JObject jObject2 = new JObject
				{
					["version"] = 2,
					["neteaseByUserId"] = jObject,
					["apple"] = _apple
				};
				AtomicFile.WriteAllText(Path_, jObject2.ToString(Formatting.Indented));
			}
			catch (Exception ex)
			{
				BridgeLog.Warn("写入昵称存档失败：" + ex.Message);
			}
		}
	}
}
