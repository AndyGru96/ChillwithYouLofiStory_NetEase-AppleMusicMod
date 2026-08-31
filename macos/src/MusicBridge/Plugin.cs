using System;
using BepInEx;
using HarmonyLib;

namespace MusicBridge;

[BepInPlugin("com.chillwithyou.musicbridge", "Chill With You Music Bridge", "1.2.0.4")]
public class Plugin : BaseUnityPlugin
{
	public const string PluginGuid = "com.chillwithyou.musicbridge";

	public const string PluginName = "Chill With You Music Bridge";

	public const string PluginVersion = "1.2.0.4";

	private Harmony _harmony;

	public static void RunOnMainThread(Action action)
	{
		MainThreadDispatcher.Enqueue(action);
	}

	private void Awake()
	{
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Expected O, but got Unknown
		BridgeLog.Init(Logger);
		BridgeLog.Info("Chill With You Music Bridge 1.2.0.4 macOS native port 正在通过 BepInEx 加载。");
		BridgeLog.Info("插件目录已就绪（BepInEx\\plugins 下）。");
		MusicBridgeOptions.Load();
		try
		{
			MainThreadDispatcher.Initialize();
			AudioPlayer.Initialize();
			AudioOutputRecovery.Initialize();
			CoverCache.Initialize();
			ITunesNameHub.LoadStorefront();
			_harmony = new Harmony("com.chillwithyou.musicbridge");
			BridgePatches.ApplyAll(_harmony);
			BridgeLog.Info("Harmony 挂钩流程结束（挂上不代表回调已执行，见后续 invoked 日志）。");
			NeteaseService.BeginRestore();
		}
		catch (Exception ex)
		{
			BridgeLog.Error("初始化失败：" + ex);
		}
	}

	private void OnDestroy()
	{
		BridgePanel.Unsubscribe();
		BridgeLog.Info("插件宿主 MonoBehaviour 被销毁（游戏的正常行为；Harmony 补丁保持有效，不做 unpatch）。");
		BridgeLog.History("销毁调用栈：" + Environment.StackTrace.Replace("\r\n", " | "));
	}
}
