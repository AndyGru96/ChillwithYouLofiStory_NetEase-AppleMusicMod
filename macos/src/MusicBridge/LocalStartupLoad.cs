using System;
using System.IO;
using System.Reflection;
using HarmonyLib;

namespace MusicBridge;

internal static class LocalStartupLoad
{
	private static bool _installed;

	private static Type _audioType;

	private static FieldInfo _fIsUnlocked;

	private static FieldInfo _fPathType;

	private static FieldInfo _fClip;

	private static FieldInfo _fTag;

	private static FieldInfo _fTitle;

	private static FieldInfo _fCredit;

	private static FieldInfo _fLocalPath;

	private static FieldInfo _fUuid;

	private static MethodInfo _mMeta;

	private static MethodInfo _mFromResult;

	private static FieldInfo _fMetaItem2;

	private static object _localPcValue;

	private static object _localTagValue;

	private static int _built;

	private static int _dropped;

	private static int _fellBack;

	private static bool _summaryLogged;

	public static bool Ready { get; private set; }

	public static void Install(Harmony harmony)
	{
		//IL_02a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02af: Expected O, but got Unknown
		//IL_02c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d6: Expected O, but got Unknown
		if (_installed)
		{
			return;
		}
		_installed = true;
		try
		{
			if (!MusicBridgeOptions.Current.Local.DeferStartupAudioLoad)
			{
				return;
			}
			if (!LocalAudioMemory.Ready)
			{
				BridgeLog.Warn("启动免解码加载：按需加载未启用（Local.UnloadUnusedAudio），不启用本项。");
				return;
			}
			_audioType = AccessTools.TypeByName("Bulbul.GameAudioInfo") ?? AccessTools.TypeByName("GameAudioInfo");
			if (_audioType == null)
			{
				BridgeLog.Warn("启动免解码加载：找不到 GameAudioInfo，跳过。");
				return;
			}
			_fIsUnlocked = AccessTools.Field(_audioType, "IsUnlocked");
			_fPathType = AccessTools.Field(_audioType, "PathType");
			_fClip = AccessTools.Field(_audioType, "AudioClip");
			_fTag = AccessTools.Field(_audioType, "Tag");
			_fTitle = AccessTools.Field(_audioType, "Title");
			_fCredit = AccessTools.Field(_audioType, "Credit");
			_fLocalPath = AccessTools.Field(_audioType, "LocalPath");
			_fUuid = AccessTools.Field(_audioType, "UUID");
			_mMeta = AccessTools.Method(_audioType, "GetAudioMetaData", new Type[1] { typeof(string) }, (Type[])null);
			MethodInfo methodInfo = AccessTools.Method(_audioType, "CreateLocalFileAsync", (Type[])null, (Type[])null);
			MethodInfo methodInfo2 = FindStartupKeepPredicate();
			MethodInfo methodInfo3 = FindUniTaskFromResult();
			if (_fIsUnlocked == null || _fPathType == null || _fClip == null || _fTag == null || _fTitle == null || _fCredit == null || _fLocalPath == null || _fUuid == null || _mMeta == null || methodInfo == null || methodInfo2 == null || methodInfo3 == null)
			{
				BridgeLog.Warn("启动免解码加载：游戏成员对不上，整体跳过（启动内存保持游戏原样）。");
				return;
			}
			_mFromResult = methodInfo3.MakeGenericMethod(_audioType);
			_localPcValue = EnumValue(_fPathType.FieldType, "LocalPc", 1);
			_localTagValue = EnumValue(_fTag.FieldType, "Local", 16);
			_fMetaItem2 = AccessTools.Field(_mMeta.ReturnType, "Item2");
			if (_localPcValue == null || _localTagValue == null || _fMetaItem2 == null)
			{
				BridgeLog.Warn("启动免解码加载：枚举值或元数据字段对不上，整体跳过。");
				return;
			}
			harmony.Patch((MethodBase)methodInfo, new HarmonyMethod(AccessTools.Method(typeof(LocalStartupLoad), "CreateLocalFile_Prefix", (Type[])null, (Type[])null)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			harmony.Patch((MethodBase)methodInfo2, new HarmonyMethod(AccessTools.Method(typeof(LocalStartupLoad), "StartupKeep_Prefix", (Type[])null, (Type[])null)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			Ready = true;
			BridgeLog.Info("启动免解码加载已启用：启动时只读文件头建曲目，音频数据等播放时再读。");
		}
		catch (Exception ex)
		{
			BridgeLog.Error("启动免解码加载安装失败：" + ex);
		}
	}

	private static MethodInfo FindStartupKeepPredicate()
	{
		Type type = AccessTools.TypeByName("Bulbul.EntryBehavior") ?? AccessTools.TypeByName("EntryBehavior");
		if (type == null)
		{
			return null;
		}
		Type nestedType = type.GetNestedType("<>c", BindingFlags.Public | BindingFlags.NonPublic);
		if (nestedType == null)
		{
			return null;
		}
		MethodInfo methodInfo = null;
		MethodInfo[] methods = nestedType.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		foreach (MethodInfo methodInfo2 in methods)
		{
			if (methodInfo2.ReturnType != typeof(bool))
			{
				continue;
			}
			ParameterInfo[] parameters = methodInfo2.GetParameters();
			if (parameters.Length == 1 && !(parameters[0].ParameterType != _audioType))
			{
				if (methodInfo != null)
				{
					return null;
				}
				methodInfo = methodInfo2;
			}
		}
		return methodInfo;
	}

	private static MethodInfo FindUniTaskFromResult()
	{
		Type type = AccessTools.TypeByName("Cysharp.Threading.Tasks.UniTask");
		if (type == null)
		{
			return null;
		}
		MethodInfo[] methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public);
		foreach (MethodInfo methodInfo in methods)
		{
			if (!(methodInfo.Name != "FromResult") && methodInfo.IsGenericMethodDefinition && methodInfo.GetParameters().Length == 1)
			{
				return methodInfo;
			}
		}
		return null;
	}

	private static object EnumValue(Type enumType, string name, int fallback)
	{
		try
		{
			if (enumType == null || !enumType.IsEnum)
			{
				return null;
			}
			if (Enum.IsDefined(enumType, name))
			{
				return Enum.Parse(enumType, name);
			}
			return Enum.ToObject(enumType, fallback);
		}
		catch
		{
			return null;
		}
	}

	private static bool CreateLocalFile_Prefix(string __0, string __1, ref object __result)
	{
		try
		{
			object obj = Activator.CreateInstance(_audioType);
			_fIsUnlocked.SetValue(obj, true);
			_fPathType.SetValue(obj, _localPcValue);
			_fTag.SetValue(obj, _localTagValue);
			_fClip.SetValue(obj, null);
			_fTitle.SetValue(obj, TitleOf(__0));
			_fCredit.SetValue(obj, CreditOf(__0));
			_fLocalPath.SetValue(obj, __0);
			_fUuid.SetValue(obj, __1);
			__result = _mFromResult.Invoke(null, new object[1] { obj });
			_built++;
			return false;
		}
		catch (Exception ex)
		{
			_fellBack++;
			if (_fellBack == 1)
			{
				BridgeLog.Warn("启动免解码建曲目失败，改由游戏原样加载：" + ex.Message);
			}
			return true;
		}
	}

	private static string TitleOf(string path)
	{
		try
		{
			if (string.IsNullOrEmpty(path))
			{
				return "";
			}
			return Path.GetFileNameWithoutExtension(path) ?? "";
		}
		catch
		{
			return "";
		}
	}

	private static string CreditOf(string path)
	{
		try
		{
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
			{
				return "";
			}
			object obj = _mMeta.Invoke(null, new object[1] { path });
			if (obj == null)
			{
				return "";
			}
			return (_fMetaItem2.GetValue(obj) as string) ?? "";
		}
		catch
		{
			return "";
		}
	}

	private static bool StartupKeep_Prefix(object __0, ref bool __result)
	{
		try
		{
			if (__0 == null)
			{
				__result = false;
				return false;
			}
			object value = _fPathType.GetValue(__0);
			if (value == null || Convert.ToInt32(value) != Convert.ToInt32(_localPcValue))
			{
				return true;
			}
			string text = _fLocalPath.GetValue(__0) as string;
			bool flag = false;
			if (!string.IsNullOrEmpty(text))
			{
				try
				{
					FileInfo fileInfo = new FileInfo(text);
					flag = fileInfo.Exists && fileInfo.Length > 0;
				}
				catch
				{
					flag = false;
				}
			}
			if (!flag)
			{
				_dropped++;
			}
			__result = flag;
			return false;
		}
		catch
		{
			return true;
		}
	}

	public static void LogSummaryOnce()
	{
		if (!Ready || _summaryLogged)
		{
			return;
		}
		_summaryLogged = true;
		if (_built != 0 || _fellBack != 0)
		{
			string text = "启动免解码加载：建立 " + _built + " 首曲目未解码音频";
			if (_dropped > 0)
			{
				text = text + "，文件已不存在而未列入 " + _dropped + " 首";
			}
			if (_fellBack > 0)
			{
				text = text + "，回退到原生加载 " + _fellBack + " 首";
			}
			BridgeLog.Info(text + "。");
		}
	}
}
