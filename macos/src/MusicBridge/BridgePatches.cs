using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace MusicBridge;

internal static class BridgePatches
{
	private static readonly Dictionary<string, bool> Caps = new Dictionary<string, bool>();

	private static readonly List<string> Failures = new List<string>();

	private static MethodInfo _uniTaskFromResultBool;

	public static bool Has(string key)
	{
		bool value;
		return Caps.TryGetValue(key, out value) && value;
	}

	public static void ApplyAll(Harmony harmony)
	{
		Caps.Clear();
		Failures.Clear();
		Patch(harmony, new string[2] { "Bulbul.MusicPlayListView", "MusicPlayListView" }, "Setup", null, "MusicPlayListView_Setup_Postfix", -1);
		Patch(harmony, new string[2] { "MusicUI", "Bulbul.MusicUI" }, "ActivatePlayList", null, "MusicUI_ActivatePlayList_Postfix");
		Patch(harmony, new string[2] { "MusicUI", "Bulbul.MusicUI" }, "DeactivatePlayList", null, "MusicUI_DeactivatePlayList_Postfix");
		Patch(harmony, new string[2] { "Bulbul.FacilityMusic", "FacilityMusic" }, "get_IsMusicEmpty", null, "FacilityMusic_IsMusicEmpty_Postfix");
		Patch(harmony, new string[2] { "MusicService", "Bulbul.MusicService" }, "get_IsAllExcludedMusicFromPlaylist", null, "MusicService_IsAllExcluded_Postfix");
		Patch(harmony, new string[2] { "MusicService", "Bulbul.MusicService" }, "SetMusicProgress", "MusicService_SetMusicProgress_Prefix", null, 1);
		Patch(harmony, new string[2] { "MusicService", "Bulbul.MusicService" }, "PlayNextMusic", "MusicService_PlayNextMusic_Prefix", null, 2);
		Patch(harmony, new string[2] { "Bulbul.FacilityManagerForPC", "FacilityManagerForPC" }, "OnClickButtonPlayListPlayMusicButton", "FacilityManagerForPC_AutoStart_Prefix", null, 1);
		Patch(harmony, new string[2] { "MusicService", "Bulbul.MusicService" }, "PlayMusicInPlaylist", null, "MusicService_GameStartedPlaying_Postfix", -1);
		Patch(harmony, new string[2] { "MusicService", "Bulbul.MusicService" }, "PlayArugumentMusic", null, "MusicService_GameStartedPlaying_Postfix", -1);
		Patch(harmony, new string[2] { "MusicService", "Bulbul.MusicService" }, "UnPause", null, "MusicService_GameStartedPlaying_Postfix", -1);
		Patch(harmony, new string[2] { "Bulbul.FacilityMusic", "FacilityMusic" }, "OnClickButtonPlayOrPauseMusic", "FacilityMusic_PlayPause_Prefix");
		Patch(harmony, new string[2] { "Bulbul.FacilityMusic", "FacilityMusic" }, "OnClickButtonSkip", "FacilityMusic_Skip_Prefix");
		Patch(harmony, new string[2] { "Bulbul.FacilityMusic", "FacilityMusic" }, "OnClickButtonBack", "FacilityMusic_Back_Prefix");
		Patch(harmony, new string[2] { "Bulbul.FacilityMusic", "FacilityMusic" }, "OnClickButtonShuffleChange", "FacilityMusic_Shuffle_Prefix");
		Patch(harmony, new string[2] { "Bulbul.FacilityMusic", "FacilityMusic" }, "OnClickButtonChangeLoop", "FacilityMusic_Loop_Prefix");
		Patch(harmony, new string[2] { "MusicUI", "Bulbul.MusicUI" }, "UpdateProgressBar", "MusicUI_UpdateProgressBar_Prefix", null, -1);
		Patch(harmony, new string[2] { "Bulbul.ExitUI", "ExitUI" }, "OnClickButtonExitGameIcon", null, "ExitUI_ExitGameIconClicked_Postfix");
		Patch(harmony, new string[2] { "Bulbul.RoomGameManager", "RoomGameManager" }, "ExitGame", "RoomGameManager_ExitGame_Prefix", null, -1);
		Patch(harmony, new string[2] { "Bulbul.RoomGameManager", "RoomGameManager" }, "ReadyPlayStory", "RoomGameManager_StoryBegin_Prefix", null, -1);
		Patch(harmony, new string[2] { "Bulbul.RoomGameManager", "RoomGameManager" }, "EndPlayedStoryForNormal", null, "RoomGameManager_StoryEnd_Postfix", -1);
		Patch(harmony, new string[2] { "Bulbul.RoomGameManager", "RoomGameManager" }, "EndPlayedStoryForLastStory", null, "RoomGameManager_StoryEnd_Postfix", -1);
		LocalPersistence.Install(harmony);
		if (MusicBridgeOptions.Current.Local.VirtualizeNativeList)
		{
			NativeListVirtualizer.Install(harmony);
		}
		LocalTrackNumbering.Install(harmony);
		LocalAudioMemory.Install(harmony);
		LocalStartupLoad.Install(harmony);
		LocalImportLoad.Install(harmony);
		if (MusicBridgeOptions.Current.Local.UnlimitedImport)
		{
			LocalImportLimit.Install(harmony);
			LocalImportPolicy.Enable();
		}
		BridgeLog.Info("本地音乐超额导入：" + LocalImportPolicy.Describe() + "。");
		LogCapabilities();
	}

	private static void Patch(Harmony harmony, string[] typeCandidates, string methodName, string prefix = null, string postfix = null, int parameterCount = 0)
	{
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		string key = typeCandidates[0] + "." + methodName;
		try
		{
			Type type = null;
			for (int i = 0; i < typeCandidates.Length; i++)
			{
				type = AccessTools.TypeByName(typeCandidates[i]);
				if (type != null)
				{
					break;
				}
			}
			if (type == null)
			{
				Fail(key, "类型不存在");
				return;
			}
			MethodBase methodBase = null;
			MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			foreach (MethodInfo methodInfo in methods)
			{
				if (!(methodInfo.Name != methodName) && (parameterCount < 0 || methodInfo.GetParameters().Length == parameterCount))
				{
					if (methodBase != null)
					{
						Fail(key, "存在多个同名同参数量重载，拒绝猜测签名");
						return;
					}
					methodBase = methodInfo;
				}
			}
			if (methodBase == null)
			{
				Fail(key, "方法不存在");
				return;
			}
			if (methodName.StartsWith("get_", StringComparison.Ordinal) && ((MethodInfo)methodBase).ReturnType != typeof(bool))
			{
				Fail(key, "getter 返回类型不是 bool");
				return;
			}
			if (methodName == "SetMusicProgress" && ((MethodInfo)methodBase).GetParameters()[0].ParameterType != typeof(float))
			{
				Fail(key, "SetMusicProgress 参数不是 float");
				return;
			}
			HarmonyMethod val = ((prefix == null) ? ((HarmonyMethod)null) : new HarmonyMethod(AccessTools.Method(typeof(BridgePatches), prefix, (Type[])null, (Type[])null)));
			HarmonyMethod val2 = ((postfix == null) ? ((HarmonyMethod)null) : new HarmonyMethod(AccessTools.Method(typeof(BridgePatches), postfix, (Type[])null, (Type[])null)));
			harmony.Patch(methodBase, val, val2, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			Caps[key] = true;
		}
		catch (Exception ex)
		{
			Fail(key, ex.GetType().Name + ": " + ex.Message);
		}
	}

	private static void Fail(string key, string reason)
	{
		Caps[key] = false;
		Failures.Add(key + " —— " + reason);
		BridgeLog.Warn("挂钩失败：" + key + " —— " + reason);
	}

	private static void LogCapabilities()
	{
		int num = 0;
		foreach (KeyValuePair<string, bool> cap in Caps)
		{
			if (cap.Value)
			{
				num++;
			}
		}
		BridgeLog.Info("Harmony 挂钩结果：" + num + " / " + Caps.Count + " 成功。");
		if (Failures.Count == 0)
		{
			BridgeLog.Info("全部挂钩点均已就位（游戏版本兼容）。");
			return;
		}
		BridgeLog.Warn("以下挂钩点缺失，对应功能会失效，但其余功能不受影响：");
		foreach (string failure in Failures)
		{
			BridgeLog.Warn("  · " + failure);
		}
	}

	private static void MusicPlayListView_Setup_Postfix(object __instance)
	{
		try
		{
			BridgeLog.Info("回调触发：MusicPlayListView.Setup invoked.");
			ScrollRect scrollRect = Traverse.Create(__instance).Field("_scrollRect").GetValue<ScrollRect>();
			GameObject gameObject = Traverse.Create(__instance).Field("_playListButtonsParent").GetValue<GameObject>();
			if (scrollRect == null)
			{
				Component component = __instance as Component;
				if (component != null)
				{
					scrollRect = component.GetComponentInChildren<ScrollRect>(includeInactive: true);
				}
				if (scrollRect != null)
				{
					BridgeLog.Info("_scrollRect 字段缺失，已按组件类型兜底找到。");
				}
			}
			if (gameObject == null && scrollRect != null && scrollRect.content != null)
			{
				gameObject = scrollRect.content.gameObject;
				BridgeLog.Info("_playListButtonsParent 字段缺失，已用 ScrollRect.content 兜底。");
			}
			if (gameObject == null)
			{
				BridgeLog.Warn("找不到播放列表容器，跳过注入。");
				return;
			}
			BridgePanel.Inject(scrollRect, gameObject);
			BridgeLog.Info("场景内 MusicBridgeSection 数量 = " + BridgePanel.CountSectionsInScene());
		}
		catch (Exception ex)
		{
			BridgeLog.Error("MusicPlayListView.Setup 后处理失败：" + ex);
		}
	}

	private static void MusicUI_ActivatePlayList_Postfix(object __instance)
	{
		try
		{
			BridgeLog.Info("回调触发：MusicUI.ActivatePlayList invoked.");
			GameNowPlayingBar.Attach(__instance);
			BridgePanel.OnPlaylistActivated();
		}
		catch (Exception ex)
		{
			BridgeLog.Error("MusicUI.ActivatePlayList 后处理失败：" + ex);
		}
	}

	private static void MusicUI_DeactivatePlayList_Postfix()
	{
		try
		{
			BridgeLog.Info("回调触发：MusicUI.DeactivatePlayList invoked.");
			BridgePanel.OnPlaylistDeactivated();
		}
		catch (Exception ex)
		{
			BridgeLog.Error("MusicUI.DeactivatePlayList 后处理失败：" + ex);
		}
	}

	private static void PauseEverything(string reason)
	{
		IMusicModule[] all = MusicModules.All;
		foreach (IMusicModule musicModule in all)
		{
			try
			{
				musicModule.PauseIfPlaying();
			}
			catch (Exception ex)
			{
				BridgeLog.Warn("退出时暂停 " + PlaybackCoordinator.Label(musicModule.Id) + " 失败：" + ex.Message);
			}
		}
		BridgeLog.Info(reason + "：已请求全部音源停止播放。");
	}

	private static void ExitUI_ExitGameIconClicked_Postfix()
	{
		PauseEverything("用户点击退出游戏图标");
	}

	private static bool RoomGameManager_ExitGame_Prefix()
	{
		PauseEverything("游戏正在退出");
		return true;
	}

	private static bool GameOwnsAudio()
	{
		return PlaybackCoordinator.Active == MusicProvider.GameBuiltIn;
	}

	private static bool TakeOver()
	{
		if (PlaybackCoordinator.Active == MusicProvider.Netease)
		{
			return MusicModules.Netease.HasTrack;
		}
		return false;
	}

	private static bool AppleMode()
	{
		if (PlaybackCoordinator.Active == MusicProvider.AppleMusic)
		{
			return MusicModules.Apple.HasTrack;
		}
		return false;
	}

	private static bool FacilityManagerForPC_AutoStart_Prefix()
	{
		try
		{
			if (!PlaybackCoordinator.ShouldSuppressAutoStart())
			{
				return true;
			}
			PlaybackCoordinator.NoteAutoStartSuppressed("FacilityManagerForPC（教程流程）");
			return false;
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("拦截进房自动起播失败，交还原生：" + ex.Message);
			return true;
		}
	}

	private static bool MusicService_PlayNextMusic_Prefix(ref object __result)
	{
		try
		{
			if (!PlaybackCoordinator.ShouldSuppressAutoStart())
			{
				return true;
			}
			if (LocalMusicSource.Invoking)
			{
				return true;
			}
			if (LocalAudioMemory.Replaying)
			{
				return true;
			}
			object obj = CompletedFalseUniTask();
			if (obj == null)
			{
				return true;
			}
			__result = obj;
			PlaybackCoordinator.NoteAutoStartSuppressed("MusicService.PlayNextMusic");
			return false;
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("拦截进房自动续播失败，交还原生：" + ex.Message);
			return true;
		}
	}

	private static object CompletedFalseUniTask()
	{
		try
		{
			if (_uniTaskFromResultBool == null)
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
						_uniTaskFromResultBool = methodInfo.MakeGenericMethod(typeof(bool));
						break;
					}
				}
			}
			if (_uniTaskFromResultBool == null)
			{
				return null;
			}
			return _uniTaskFromResultBool.Invoke(null, new object[1] { false });
		}
		catch
		{
			return null;
		}
	}

	private static void MusicService_GameStartedPlaying_Postfix(object __instance, object[] __args)
	{
		LocalMusicSource.Service = __instance;
		bool num = IsAutoChange(__args);
		if (!num)
		{
			PlaybackCoordinator.MarkUserChose();
		}
		else
		{
			PlaybackCoordinator.NoteGameAutoStarted();
		}
		IMusicModule current = MusicModules.Current;
		bool flag = current.Id != MusicProvider.GameBuiltIn && current.IsPlaying;
		if (num && flag)
		{
			BridgeLog.Info("游戏自动起播，但你正在听" + PlaybackCoordinator.Label(current.Id) + "，本次不交出发声权。");
			try
			{
				MusicModules.Game.PauseIfPlaying();
				return;
			}
			catch (Exception ex)
			{
				BridgeLog.Warn("压制游戏自动起播失败：" + ex.Message);
				return;
			}
		}
		BridgePanel.ClaimAudio(MusicProvider.GameBuiltIn);
	}

	private static bool IsAutoChange(object[] args)
	{
		if (args != null)
		{
			foreach (object obj in args)
			{
				if (obj == null)
				{
					continue;
				}
				Type type = obj.GetType();
				if (type.IsEnum && !(type.Name != "MusicChangeKind"))
				{
					try
					{
						return Convert.ToInt32(obj) == 0;
					}
					catch
					{
						return false;
					}
				}
			}
		}
		return true;
	}

	private static void RoomGameManager_StoryBegin_Prefix()
	{
		PlaybackCoordinator.BeginStoryYield();
	}

	private static void RoomGameManager_StoryEnd_Postfix()
	{
		PlaybackCoordinator.EndStoryYield();
	}

	private static void FacilityMusic_IsMusicEmpty_Postfix(object __instance, ref bool __result)
	{
		if (__instance != null)
		{
			LocalMusicSource.Facility = __instance;
		}
		if (TakeOver() || AppleMode())
		{
			__result = false;
		}
	}

	private static void MusicService_IsAllExcluded_Postfix(object __instance, ref bool __result)
	{
		if (__instance != null && LocalMusicSource.Service == null)
		{
			LocalMusicSource.Service = __instance;
		}
		if (TakeOver() || AppleMode())
		{
			__result = false;
		}
	}

	private static bool MusicService_SetMusicProgress_Prefix(float progress)
	{
		if (LocalMusicSource.Invoking)
		{
			return true;
		}
		if (!TakeOver())
		{
			return true;
		}
		AudioPlayer instance = AudioPlayer.Instance;
		float durationSeconds = instance.DurationSeconds;
		if (durationSeconds > 0f)
		{
			float seconds = Mathf.Clamp01(progress) * durationSeconds;
			BridgeLog.Info("游戏底部进度条拖动 -> MusicBridge 跳转到 " + seconds.ToString("0.0") + "s");
			instance.Seek(seconds);
		}
		return false;
	}

	private static bool FacilityMusic_PlayPause_Prefix()
	{
		if (LocalMusicSource.Invoking)
		{
			return true;
		}
		return MusicTransport.HandleGameButton(TransportAction.PlayPause);
	}

	private static bool FacilityMusic_Skip_Prefix()
	{
		if (LocalMusicSource.Invoking)
		{
			return true;
		}
		return MusicTransport.HandleGameButton(TransportAction.Next);
	}

	private static bool FacilityMusic_Back_Prefix()
	{
		if (LocalMusicSource.Invoking)
		{
			return true;
		}
		return MusicTransport.HandleGameButton(TransportAction.Previous);
	}

	private static bool FacilityMusic_Shuffle_Prefix()
	{
		if (LocalMusicSource.Invoking)
		{
			return true;
		}
		return MusicTransport.HandleGameButton(TransportAction.Shuffle);
	}

	private static bool FacilityMusic_Loop_Prefix()
	{
		if (LocalMusicSource.Invoking)
		{
			return true;
		}
		return MusicTransport.HandleGameButton(TransportAction.RepeatOne);
	}

	private static bool MusicUI_UpdateProgressBar_Prefix()
	{
		if (!TakeOver())
		{
			return !AppleMode();
		}
		return false;
	}

	internal static void DirectPlayPause()
	{
		FacilityMusic_PlayPause_Prefix();
	}

	internal static void DirectNext()
	{
		FacilityMusic_Skip_Prefix();
	}

	internal static void DirectPrevious()
	{
		FacilityMusic_Back_Prefix();
	}

	internal static void DirectShuffle()
	{
		FacilityMusic_Shuffle_Prefix();
	}

	internal static void DirectLoop()
	{
		FacilityMusic_Loop_Prefix();
	}
}
