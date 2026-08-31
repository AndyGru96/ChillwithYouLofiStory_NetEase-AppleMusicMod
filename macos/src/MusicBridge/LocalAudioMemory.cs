using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using HarmonyLib;
using UnityEngine;

namespace MusicBridge;

internal static class LocalAudioMemory
{
	private struct ProcessMemoryCounters
	{
		public uint cb;

		public uint PageFaultCount;

		public UIntPtr PeakWorkingSetSize;

		public UIntPtr WorkingSetSize;

		public UIntPtr QuotaPeakPagedPoolUsage;

		public UIntPtr QuotaPagedPoolUsage;

		public UIntPtr QuotaPeakNonPagedPoolUsage;

		public UIntPtr QuotaNonPagedPoolUsage;

		public UIntPtr PagefileUsage;

		public UIntPtr PeakPagefileUsage;

		public UIntPtr PrivateUsage;
	}

	private static bool _installed;

	private static Type _audioType;

	private static Type _serviceType;

	private static FieldInfo _fClip;

	private static FieldInfo _fPathType;

	private static FieldInfo _fLocalPath;

	private static MethodInfo _mGetAudioClip;

	private static MethodInfo _mUnload;

	private static MethodInfo _mPlayInPlaylist;

	private static MethodInfo _mPlayArgument;

	private static MethodInfo _mGetPlayList;

	private static MethodInfo _mGetPlaying;

	private static readonly List<object> _recent = new List<object>();

	private static object _awaiting;

	private static Action _resume;

	private static int _awaitFrames;

	private static bool _replaying;

	private const int MaxAwaitFrames = 600;

	private static readonly Dictionary<string, long> SizeCache = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

	private static readonly List<long> _sizeBuffer = new List<long>();

	private static int _startupFrames;

	private static bool _startupReleased;

	private static long _beforeMb;

	private static int _reportAtFrame;

	private static int _sweepFrames;

	private const int SweepIntervalFrames = 1800;

	public static bool Ready { get; private set; }

	internal static bool Replaying => _replaying;

	private static int BudgetCount
	{
		get
		{
			int loadedClipBudget = MusicBridgeOptions.Current.Local.LoadedClipBudget;
			if (loadedClipBudget >= 2)
			{
				return loadedClipBudget;
			}
			return 2;
		}
	}

	private static long BudgetBytes => (long)MusicBridgeOptions.Current.Local.LoadedClipBudgetMegabytes * 1024L * 1024;

	private static long SizeOf(object track)
	{
		try
		{
			string text = ((_fLocalPath != null) ? (_fLocalPath.GetValue(track) as string) : null);
			if (string.IsNullOrEmpty(text))
			{
				return 0L;
			}
			if (SizeCache.TryGetValue(text, out var value))
			{
				return value;
			}
			try
			{
				FileInfo fileInfo = new FileInfo(text);
				value = (fileInfo.Exists ? fileInfo.Length : 0);
			}
			catch
			{
				value = 0L;
			}
			if (SizeCache.Count >= 4096)
			{
				SizeCache.Clear();
			}
			SizeCache[text] = value;
			return value;
		}
		catch
		{
			return 0L;
		}
	}

	public static void Install(Harmony harmony)
	{
		//IL_01d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e4: Expected O, but got Unknown
		//IL_0201: Unknown result type (might be due to invalid IL or missing references)
		//IL_020f: Expected O, but got Unknown
		if (_installed)
		{
			return;
		}
		_installed = true;
		try
		{
			if (!MusicBridgeOptions.Current.Local.UnloadUnusedAudio)
			{
				return;
			}
			_audioType = AccessTools.TypeByName("Bulbul.GameAudioInfo") ?? AccessTools.TypeByName("GameAudioInfo");
			_serviceType = AccessTools.TypeByName("MusicService") ?? AccessTools.TypeByName("Bulbul.MusicService");
			if (_audioType == null || _serviceType == null)
			{
				BridgeLog.Warn("音频内存管理：找不到游戏类型，不启用。");
				return;
			}
			_fClip = AccessTools.Field(_audioType, "AudioClip");
			_fPathType = AccessTools.Field(_audioType, "PathType");
			_fLocalPath = AccessTools.Field(_audioType, "LocalPath");
			_mGetAudioClip = AccessTools.Method(_audioType, "GetAudioClip", (Type[])null, (Type[])null);
			_mUnload = AccessTools.Method(_audioType, "UnloadAudioClip", (Type[])null, (Type[])null);
			_mPlayInPlaylist = AccessTools.Method(_serviceType, "PlayMusicInPlaylist", (Type[])null, (Type[])null);
			_mPlayArgument = AccessTools.Method(_serviceType, "PlayArugumentMusic", (Type[])null, (Type[])null);
			_mGetPlayList = AccessTools.PropertyGetter(_serviceType, "CurrentPlayList");
			_mGetPlaying = AccessTools.PropertyGetter(_serviceType, "PlayingMusic");
			if (_fClip == null || _fPathType == null || _mGetAudioClip == null || _mUnload == null || _mPlayInPlaylist == null || _mPlayArgument == null || _mGetPlayList == null || _mGetPlaying == null)
			{
				BridgeLog.Warn("音频内存管理：游戏成员对不上，不启用。");
				return;
			}
			harmony.Patch((MethodBase)_mPlayInPlaylist, new HarmonyMethod(AccessTools.Method(typeof(LocalAudioMemory), "PlayInPlaylist_Prefix", (Type[])null, (Type[])null)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			harmony.Patch((MethodBase)_mPlayArgument, new HarmonyMethod(AccessTools.Method(typeof(LocalAudioMemory), "PlayArgument_Prefix", (Type[])null, (Type[])null)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			Ready = true;
			BridgeLog.Info("音频内存管理已启用：最多常驻 " + BudgetCount + " 首 / " + BudgetBytes / 1024 / 1024 + " MB 本地曲目的音频数据，其余按需加载。");
		}
		catch (Exception ex)
		{
			BridgeLog.Error("音频内存管理安装失败：" + ex);
		}
	}

	private static bool PlayInPlaylist_Prefix(object __instance, int __0)
	{
		object target = TrackAt(__instance, __0);
		int index = __0;
		return Gate(__instance, target, delegate
		{
			_mPlayInPlaylist.Invoke(__instance, new object[1] { index });
		});
	}

	private static bool PlayArgument_Prefix(object __instance, object __0, object __1)
	{
		object a0 = __0;
		return Gate(__instance, __0, delegate
		{
			_mPlayArgument.Invoke(__instance, new object[2] { a0, __1 });
		});
	}

	private static bool Gate(object service, object target, Action replay)
	{
		try
		{
			if (!Ready || target == null)
			{
				return true;
			}
			if (_replaying)
			{
				return true;
			}
			if (!IsLocal(target))
			{
				return true;
			}
			LocalImportLoad.ClearStubOn(target);
			Touch(target);
			TrimBudget(service);
			if (ClipOf(target) != null)
			{
				return true;
			}
			BeginLoad(target, replay);
			return false;
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("音频按需加载前置失败，交还原生：" + ex.Message);
			return true;
		}
	}

	private static void BeginLoad(object target, Action replay)
	{
		_awaiting = target;
		_resume = replay;
		_awaitFrames = 0;
		try
		{
			_mGetAudioClip.Invoke(target, new object[1] { default(CancellationToken) });
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("发起音频重载失败：" + ex.Message);
			_awaiting = null;
			_resume = null;
			try
			{
				replay();
			}
			catch
			{
			}
		}
	}

	public static void Tick()
	{
		if (!Ready)
		{
			return;
		}
		if (!_startupReleased && LocalMusicSource.Service != null && ++_startupFrames > 600)
		{
			_startupReleased = true;
			_beforeMb = WorkingSetMb();
			ReleaseAllButBudget(LocalMusicSource.Service, startup: true);
			LocalStartupLoad.LogSummaryOnce();
			_reportAtFrame = _startupFrames + 1800;
		}
		if (_reportAtFrame > 0 && ++_startupFrames >= _reportAtFrame)
		{
			_reportAtFrame = 0;
			BridgeLog.Info("音频内存：启动回收前工作集 " + _beforeMb + " MB，30 秒后 " + WorkingSetMb() + " MB。");
		}
		if (_startupReleased && ++_sweepFrames >= 1800)
		{
			_sweepFrames = 0;
			object service = LocalMusicSource.Service;
			if (service != null)
			{
				try
				{
					object obj = _mGetPlaying.Invoke(service, null);
					if (obj != null && IsLocal(obj))
					{
						Touch(obj);
					}
				}
				catch
				{
				}
				TrimBudget(service);
				ReleaseAllButBudget(service, startup: false);
			}
		}
		if (_awaiting == null)
		{
			return;
		}
		if (ClipOf(_awaiting) != null)
		{
			Action resume = _resume;
			_awaiting = null;
			_resume = null;
			_replaying = true;
			try
			{
				resume?.Invoke();
				return;
			}
			catch (Exception ex)
			{
				BridgeLog.Warn("音频加载完成后重放失败：" + ex.Message);
				return;
			}
			finally
			{
				_replaying = false;
			}
		}
		if (++_awaitFrames <= 600)
		{
			return;
		}
		Action resume2 = _resume;
		_awaiting = null;
		_resume = null;
		BridgeLog.Warn("音频重载超时（10 秒），仍按原样播放一次，交给游戏自己处理。");
		_replaying = true;
		try
		{
			resume2?.Invoke();
		}
		catch
		{
		}
		finally
		{
			_replaying = false;
		}
	}

	private static void Touch(object track)
	{
		for (int num = _recent.Count - 1; num >= 0; num--)
		{
			if (_recent[num] == track)
			{
				_recent.RemoveAt(num);
				break;
			}
		}
		_recent.Add(track);
	}

	private static void TrimBudget(object service)
	{
		object obj = null;
		try
		{
			if (service != null)
			{
				obj = _mGetPlaying.Invoke(service, null);
			}
		}
		catch
		{
		}
		int budgetCount = BudgetCount;
		long budgetBytes = BudgetBytes;
		_sizeBuffer.Clear();
		for (int i = 0; i < _recent.Count; i++)
		{
			_sizeBuffer.Add(SizeOf(_recent[i]));
		}
		int num = LocalClipBudget.ComputeCut(_sizeBuffer, budgetCount, budgetBytes);
		if (num <= 0)
		{
			return;
		}
		int num2 = 0;
		for (int j = 0; j < num; j++)
		{
			object obj3 = _recent[j];
			if (obj3 != null && obj3 != obj && IsLocal(obj3) && !(ClipOf(obj3) == null))
			{
				try
				{
					_mUnload.Invoke(obj3, null);
					num2++;
				}
				catch (Exception ex)
				{
					BridgeLog.Warn("卸载音频失败：" + ex.Message);
				}
			}
		}
		_recent.RemoveRange(0, num);
		if (num2 > 0)
		{
			BridgeLog.Info("音频内存：已卸载 " + num2 + " 首不再使用的曲目（常驻上限 " + budgetCount + " 首 / " + budgetBytes / 1024 / 1024 + " MB）。");
		}
	}

	public static void ReleaseAllButBudget(object service, bool startup)
	{
		if (!Ready || service == null)
		{
			return;
		}
		try
		{
			object obj = _mGetPlayList.Invoke(service, null);
			if (obj == null)
			{
				return;
			}
			PropertyInfo propertyInfo = AccessTools.Property(obj.GetType(), "Count");
			MethodInfo methodInfo = AccessTools.Method(obj.GetType(), "get_Item", new Type[1] { typeof(int) }, (Type[])null);
			if (propertyInfo == null || methodInfo == null)
			{
				return;
			}
			object obj2 = null;
			try
			{
				obj2 = _mGetPlaying.Invoke(service, null);
			}
			catch
			{
			}
			int num = (int)propertyInfo.GetValue(obj, null);
			int num2 = 0;
			for (int i = 0; i < num; i++)
			{
				object obj4 = methodInfo.Invoke(obj, new object[1] { i });
				if (obj4 == null || obj4 == obj2 || !IsLocal(obj4) || ClipOf(obj4) == null)
				{
					continue;
				}
				bool flag = false;
				for (int j = 0; j < _recent.Count; j++)
				{
					if (_recent[j] == obj4)
					{
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					try
					{
						_mUnload.Invoke(obj4, null);
						num2++;
					}
					catch
					{
					}
				}
			}
			if (num2 > 0)
			{
				BridgeLog.Info(startup ? ("音频内存：启动回收完成，释放 " + num2 + " 首曲目的音频数据。") : ("音频内存：周期清扫，释放 " + num2 + " 首不在常驻名单里的曲目。"));
			}
		}
		catch (Exception ex)
		{
			BridgeLog.Warn((startup ? "启动回收失败：" : "周期清扫失败：") + ex.Message);
		}
	}

	private static bool IsLocal(object track)
	{
		try
		{
			return Convert.ToInt32(_fPathType.GetValue(track)) == 1;
		}
		catch
		{
			return false;
		}
	}

	private static AudioClip ClipOf(object track)
	{
		try
		{
			return _fClip.GetValue(track) as AudioClip;
		}
		catch
		{
			return null;
		}
	}

    private static long WorkingSetMb()
    {
        try { using (var process = System.Diagnostics.Process.GetCurrentProcess()) return process.WorkingSet64 / 1024 / 1024; }
        catch { return -1; }
    }

	private static object TrackAt(object service, int index)
	{
		try
		{
			object obj = _mGetPlayList.Invoke(service, null);
			if (obj == null)
			{
				return null;
			}
			PropertyInfo propertyInfo = AccessTools.Property(obj.GetType(), "Count");
			MethodInfo methodInfo = AccessTools.Method(obj.GetType(), "get_Item", new Type[1] { typeof(int) }, (Type[])null);
			if (propertyInfo == null || methodInfo == null)
			{
				return null;
			}
			int num = (int)propertyInfo.GetValue(obj, null);
			if (index < 0 || index >= num)
			{
				return null;
			}
			return methodInfo.Invoke(obj, new object[1] { index });
		}
		catch
		{
			return null;
		}
	}
}
