using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;

namespace MusicBridge;

internal static class LocalTrackNumbering
{
	private static bool _installed;

	private static bool _fieldsResolved;

	private static FieldInfo _fTitleText;

	private static FieldInfo _fAudioUuid;

	private static FieldInfo _fAudioTitle;

	private static Dictionary<string, int> _index = new Dictionary<string, int>();

	private static int _builtFrom = -1;

	private static int _width = 3;

	public static bool Enabled => MusicBridgeOptions.Current.Local.ShowImportIndex;

	private static bool EnsureFields()
	{
		if (_fieldsResolved)
		{
			return _fAudioTitle != null;
		}
		_fieldsResolved = true;
		try
		{
			Type type = AccessTools.TypeByName("Bulbul.MusicPlayListButtons") ?? AccessTools.TypeByName("MusicPlayListButtons");
			Type type2 = AccessTools.TypeByName("Bulbul.GameAudioInfo") ?? AccessTools.TypeByName("GameAudioInfo");
			if (type != null)
			{
				_fTitleText = AccessTools.Field(type, "_musicTitleText");
			}
			if (type2 != null)
			{
				_fAudioUuid = AccessTools.Field(type2, "UUID");
				_fAudioTitle = AccessTools.Field(type2, "Title");
			}
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("导入序号：字段解析失败：" + ex.Message);
		}
		return _fAudioTitle != null;
	}

	public static void Install(Harmony harmony)
	{
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Expected O, but got Unknown
		if (_installed)
		{
			return;
		}
		_installed = true;
		try
		{
			if (!Enabled)
			{
				return;
			}
			if (!EnsureFields() || _fTitleText == null || _fAudioUuid == null)
			{
				BridgeLog.Warn("导入序号：行成员对不上，不启用。");
				return;
			}
			Type type = AccessTools.TypeByName("Bulbul.MusicPlayListButtons") ?? AccessTools.TypeByName("MusicPlayListButtons");
			MethodInfo methodInfo = ((type != null) ? AccessTools.Method(type, "Setup", (Type[])null, (Type[])null) : null);
			if (methodInfo == null)
			{
				BridgeLog.Warn("导入序号：找不到行的 Setup，不启用。");
				return;
			}
			harmony.Patch((MethodBase)methodInfo, (HarmonyMethod)null, new HarmonyMethod(AccessTools.Method(typeof(LocalTrackNumbering), "Setup_Postfix", (Type[])null, (Type[])null)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			BridgeLog.Info("导入序号已启用（只改列表显示，不写存档、不改文件）。");
		}
		catch (Exception ex)
		{
			BridgeLog.Error("导入序号安装失败：" + ex);
		}
	}

	private static void Setup_Postfix(object __instance, object __0)
	{
		try
		{
			if (Enabled && __0 != null)
			{
				TextMeshProUGUI textMeshProUGUI = _fTitleText.GetValue(__instance) as TextMeshProUGUI;
				if (!(textMeshProUGUI == null))
				{
					textMeshProUGUI.text = Decorate(__0);
				}
			}
		}
		catch
		{
		}
	}

	public static string Decorate(object audioInfo)
	{
		if (audioInfo == null)
		{
			return "";
		}
		if (!EnsureFields())
		{
			return "";
		}
		string text = _fAudioTitle.GetValue(audioInfo) as string;
		if (text == null)
		{
			text = "";
		}
		if (!Enabled || _fAudioUuid == null)
		{
			return text;
		}
		EnsureIndex();
		if (_index.Count == 0)
		{
			return text;
		}
		string text2 = _fAudioUuid.GetValue(audioInfo) as string;
		if (string.IsNullOrEmpty(text2) || !_index.TryGetValue(text2, out var value))
		{
			return text;
		}
		return "#" + value.ToString().PadLeft(_width, '0') + "  " + text;
	}

	public static int NumberOf(object audioInfo)
	{
		if (audioInfo == null || !EnsureFields() || _fAudioUuid == null)
		{
			return 0;
		}
		EnsureIndex();
		if (_index.Count == 0)
		{
			return 0;
		}
		string text = _fAudioUuid.GetValue(audioInfo) as string;
		if (string.IsNullOrEmpty(text) || !_index.TryGetValue(text, out var value))
		{
			return 0;
		}
		return value;
	}

	public static string Format(int number)
	{
		if (number <= 0)
		{
			return "";
		}
		return "#" + number.ToString().PadLeft(_width, '0');
	}

	private static void EnsureIndex()
	{
		IList list = LocalPersistence.LiveTracks();
		if (list == null)
		{
			_index.Clear();
			_builtFrom = -1;
		}
		else
		{
			if (LocalPersistence.IsProjecting || list.Count == _builtFrom)
			{
				return;
			}
			Dictionary<string, int> dictionary = new Dictionary<string, int>(list.Count);
			int num = 0;
			foreach (object item in list)
			{
				string text = LocalPersistence.UuidOf(item);
				num++;
				if (!string.IsNullOrEmpty(text) && !dictionary.ContainsKey(text))
				{
					dictionary[text] = num;
				}
			}
			_index = dictionary;
			_builtFrom = list.Count;
			_width = ((list.Count >= 1000) ? 4 : ((list.Count >= 100) ? 3 : 2));
		}
	}
}
