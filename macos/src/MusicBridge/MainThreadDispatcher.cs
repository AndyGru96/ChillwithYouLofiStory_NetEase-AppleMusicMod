using System;
using System.Collections.Generic;
using UnityEngine;

namespace MusicBridge;

internal class MainThreadDispatcher : MonoBehaviour
{
	private static MainThreadDispatcher _instance;

	private static readonly Queue<Action> Queue = new Queue<Action>();

	private static readonly object QueueLock = new object();

	private int _frames;

	private static bool _quitting;

	public static void Initialize()
	{
		if (!(_instance != null))
		{
			GameObject obj = new GameObject("MusicBridge_MainThreadDispatcher");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			obj.hideFlags = HideFlags.HideAndDontSave;
			_instance = obj.AddComponent<MainThreadDispatcher>();
			BridgeLog.Info("主线程调度器已创建。");
		}
	}

	public static void Enqueue(Action action)
	{
		if (action == null)
		{
			return;
		}
		lock (QueueLock)
		{
			Queue.Enqueue(action);
		}
	}

	private void OnDestroy()
	{
		BridgeLog.Warn("主线程调度器对象被销毁（帧数=" + _frames + "）。");
		_instance = null;
		if (!_quitting)
		{
			BridgeLog.Warn("调度器被非退出流程销毁，立即安全重建。");
			Initialize();
		}
	}

	private void OnApplicationQuit()
	{
		_quitting = true;
		BridgeLog.Info("游戏正在退出，停止所有后台轮询。");
		NeteaseService.Shutdown();
		AppleMusicService.Shutdown();

	}

	private void Update()
	{
		_frames++;
		if (_frames == 1 || _frames == 300 || _frames == 900 || _frames == 1800)
		{
			BridgeLog.Info("调度器心跳：第 " + _frames + " 帧仍在运行。");
		}

		BridgePanel.TickAlways();
		while (true)
		{
			Action action;
			lock (QueueLock)
			{
				if (Queue.Count == 0)
				{
					break;
				}
				action = Queue.Dequeue();
			}
			try
			{
				action();
			}
			catch (Exception ex)
			{
				BridgeLog.Error("主线程任务异常：" + ex);
			}
		}
	}
}
