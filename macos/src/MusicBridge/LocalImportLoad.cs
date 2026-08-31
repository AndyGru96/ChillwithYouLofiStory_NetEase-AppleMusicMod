using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using UnityEngine;

namespace MusicBridge;

internal static class LocalImportLoad
{
	private static bool _installed;

	private static FieldInfo _fClip;

	private static FieldInfo _fMetaItem2;

	private static MethodInfo _mMeta;

	private static MethodInfo _mFromResult;

	private static ConstructorInfo _tupleCtor;

	private static int _importDepth;

	private static readonly HashSet<int> _stubIds = new HashSet<int>();

	private const int MaxTrackedStubs = 8192;

	private static int _stubbed;

	private static int _passedThrough;

	public static bool Ready { get; private set; }

	public static void Install(Harmony harmony)
	{
		//IL_023e: Unknown result type (might be due to invalid IL or missing references)
		//IL_025b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0266: Expected O, but got Unknown
		//IL_0266: Expected O, but got Unknown
		//IL_027f: Unknown result type (might be due to invalid IL or missing references)
		//IL_028d: Expected O, but got Unknown
		//IL_02a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b4: Expected O, but got Unknown
		if (_installed)
		{
			return;
		}
		_installed = true;
		try
		{
			if (!MusicBridgeOptions.Current.Local.DeferImportAudioLoad)
			{
				return;
			}
			if (!LocalAudioMemory.Ready)
			{
				BridgeLog.Warn("导入免解码：按需加载未启用（Local.UnloadUnusedAudio），不启用本项。");
				return;
			}
			Type type = AccessTools.TypeByName("Bulbul.GameAudioInfo") ?? AccessTools.TypeByName("GameAudioInfo");
			Type type2 = AccessTools.TypeByName("MusicService") ?? AccessTools.TypeByName("Bulbul.MusicService");
			if (type == null || type2 == null)
			{
				BridgeLog.Warn("导入免解码：找不到游戏类型，跳过。");
				return;
			}
			_fClip = AccessTools.Field(type, "AudioClip");
			_mMeta = AccessTools.Method(type, "GetAudioMetaData", new Type[1] { typeof(string) }, (Type[])null);
			MethodInfo methodInfo = AccessTools.Method(type, "DownloadAudioFile", new Type[2]
			{
				typeof(string),
				typeof(CancellationToken)
			}, (Type[])null);
			MethodInfo methodInfo2 = AccessTools.Method(type2, "AddLocalMusicItem", (Type[])null, (Type[])null);
			MethodInfo methodInfo3 = LocalImportLimit.FindStateMachineMoveNext(type, "ImportLocalFiles");
			MethodInfo methodInfo4 = FindUniTaskFromResult();
			if (_fClip == null || _mMeta == null || methodInfo == null || methodInfo2 == null || methodInfo3 == null || methodInfo4 == null)
			{
				BridgeLog.Warn("导入免解码：游戏成员对不上，整体跳过（导入内存保持游戏原样）。");
				return;
			}
			_fMetaItem2 = AccessTools.Field(_mMeta.ReturnType, "Item2");
			Type type3 = (methodInfo.ReturnType.IsGenericType ? methodInfo.ReturnType.GetGenericArguments()[0] : null);
			if (type3 != null)
			{
				_tupleCtor = AccessTools.Constructor(type3, new Type[3]
				{
					typeof(AudioClip),
					typeof(string),
					typeof(string)
				}, false);
			}
			if (_fMetaItem2 == null || type3 == null || _tupleCtor == null)
			{
				BridgeLog.Warn("导入免解码：返回值形态与实测不符，整体跳过。");
				return;
			}
			_mFromResult = methodInfo4.MakeGenericMethod(type3);
			harmony.Patch((MethodBase)methodInfo3, new HarmonyMethod(AccessTools.Method(typeof(LocalImportLoad), "ImportMoveNext_Prefix", (Type[])null, (Type[])null)), (HarmonyMethod)null, (HarmonyMethod)null, new HarmonyMethod(AccessTools.Method(typeof(LocalImportLoad), "ImportMoveNext_Finalizer", (Type[])null, (Type[])null)), (HarmonyMethod)null);
			harmony.Patch((MethodBase)methodInfo, new HarmonyMethod(AccessTools.Method(typeof(LocalImportLoad), "Download_Prefix", (Type[])null, (Type[])null)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			harmony.Patch((MethodBase)methodInfo2, (HarmonyMethod)null, new HarmonyMethod(AccessTools.Method(typeof(LocalImportLoad), "Add_Postfix", (Type[])null, (Type[])null)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			Ready = true;
			BridgeLog.Info("导入免解码已启用：导入时只读文件头建曲目，音频数据等播放时再读。");
		}
		catch (Exception ex)
		{
			BridgeLog.Error("导入免解码安装失败：" + ex);
		}
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

	private static void ImportMoveNext_Prefix()
	{
		_importDepth++;
	}

	private static void ImportMoveNext_Finalizer()
	{
		if (_importDepth > 0)
		{
			_importDepth--;
		}
		if (_importDepth == 0)
		{
			LogBatchOnce();
		}
	}

	private static bool Download_Prefix(string __0, ref object __result)
	{
		if (_importDepth <= 0)
		{
			return true;
		}
		try
		{
			long length;
			try
			{
				FileInfo fileInfo = new FileInfo(__0);
				if (!fileInfo.Exists)
				{
					return true;
				}
				length = fileInfo.Length;
			}
			catch
			{
				return true;
			}
			if (length <= 0)
			{
				return true;
			}
			AudioClip audioClip = AudioClip.Create(Path.GetFileNameWithoutExtension(__0) ?? "clip", 1, 1, 44100, stream: false);
			if (audioClip == null)
			{
				_passedThrough++;
				return true;
			}
			object obj2 = _tupleCtor.Invoke(new object[3]
			{
				audioClip,
				"",
				CreditOf(__0)
			});
			__result = _mFromResult.Invoke(null, new object[1] { obj2 });
			if (_stubIds.Count >= 8192)
			{
				_stubIds.Clear();
			}
			_stubIds.Add(audioClip.GetInstanceID());
			_stubbed++;
			return false;
		}
		catch (Exception ex)
		{
			_passedThrough++;
			if (_passedThrough == 1)
			{
				BridgeLog.Warn("导入免解码建曲目失败，改由游戏原样加载：" + ex.Message);
			}
			return true;
		}
	}

	private static string CreditOf(string path)
	{
		try
		{
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

	private static void Add_Postfix(object __0)
	{
		ClearStubOn(__0);
	}

	public static void ClearStubOn(object track)
	{
		if (!Ready || track == null)
		{
			return;
		}
		try
		{
			AudioClip audioClip = _fClip.GetValue(track) as AudioClip;
			if (!(audioClip == null) && IsStub(audioClip))
			{
				_fClip.SetValue(track, null);
				Drop(audioClip);
			}
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("摘除导入占位失败：" + ex.Message);
		}
	}

	public static bool IsStub(AudioClip clip)
	{
		if (!Ready || clip == null)
		{
			return false;
		}
		try
		{
			if (_stubIds.Contains(clip.GetInstanceID()))
			{
				return true;
			}
			return clip.samples <= 1;
		}
		catch
		{
			return false;
		}
	}

	private static void Drop(AudioClip clip)
	{
		try
		{
			_stubIds.Remove(clip.GetInstanceID());
			UnityEngine.Object.Destroy(clip);
		}
		catch
		{
		}
	}

	private static void LogBatchOnce()
	{
		if (_stubbed != 0 || _passedThrough != 0)
		{
			string text = "导入免解码：本次导入 " + _stubbed + " 首未解码音频";
			if (_passedThrough > 0)
			{
				text = text + "，回退到原样解码 " + _passedThrough + " 首";
			}
			BridgeLog.Info(text + "。");
			_stubbed = 0;
			_passedThrough = 0;
		}
	}
}
