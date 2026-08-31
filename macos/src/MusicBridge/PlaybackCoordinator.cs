using System;
using System.Collections.Generic;
using UnityEngine;

namespace MusicBridge;

internal static class PlaybackCoordinator
{
	private static MusicProvider _active = MusicProvider.Netease;

	private static float _switchingUntil;

	internal static bool PendingAutoPause;

	private static readonly HashSet<string> SuppressLogged = new HashSet<string>();

	private static bool _yieldedForStory;

	private static MusicProvider _storyYieldWho;

	public static MusicProvider Active => _active;

	public static bool IsSwitching => Time.unscaledTime < _switchingUntil;

	public static bool UserHasChosen { get; private set; }

	public static void NoteSwitching(float seconds)
	{
		float num = Time.unscaledTime + seconds;
		if (num > _switchingUntil)
		{
			_switchingUntil = num;
		}
	}

	public static void MarkUserChose()
	{
		if (!UserHasChosen)
		{
			UserHasChosen = true;
			PendingAutoPause = false;
		}
	}

	internal static bool ShouldSuppressAutoStart()
	{
		if (UserHasChosen)
		{
			return false;
		}
		return MusicBridgeOptions.Current.Shared.PauseGameMusicUntilUserChooses;
	}

	internal static void NoteAutoStartSuppressed(string source)
	{
		if (SuppressLogged.Add(source))
		{
			BridgeLog.Info("游戏进房的自动播放已在出声前拦下（拦截点：" + source + "），等待你选择音源。（不想要这个行为可关闭 Shared.PauseGameMusicUntilUserChooses）");
		}
	}

	internal static void NoteGameAutoStarted()
	{
		if (!UserHasChosen && MusicBridgeOptions.Current.Shared.PauseGameMusicUntilUserChooses)
		{
			PendingAutoPause = true;
		}
	}

	internal static void TickAutoPause()
	{
		if (!PendingAutoPause)
		{
			return;
		}
		PendingAutoPause = false;
		if (UserHasChosen)
		{
			return;
		}
		try
		{
			MusicModules.Game.PauseIfPlaying();
			string text = "";
			BridgeLog.Info("游戏进房时自动播放了自带音乐，已按配置暂停，等待你选择音源。" + text + "（不想要这个行为可关闭 Shared.PauseGameMusicUntilUserChooses）");
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("抑制游戏自动播放失败：" + ex.Message);
		}
	}

	internal static void BeginStoryYield()
	{
		if (_yieldedForStory)
		{
			return;
		}
		IMusicModule current = MusicModules.Current;
		if (current.Id == MusicProvider.GameBuiltIn || !current.IsPlaying)
		{
			return;
		}
		_yieldedForStory = true;
		_storyYieldWho = current.Id;
		try
		{
			current.PauseIfPlaying();
			BridgeLog.Info("剧情开始：" + Label(current.Id) + " 已暂停让路，结束后自动恢复。");
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("剧情让路失败：" + ex.Message);
		}
	}

	internal static void EndStoryYield()
	{
		if (!_yieldedForStory)
		{
			return;
		}
		_yieldedForStory = false;
		MusicProvider storyYieldWho = _storyYieldWho;
		try
		{
			IMusicModule current = MusicModules.Current;
			if (current.Id != storyYieldWho && current.Id != MusicProvider.GameBuiltIn && current.IsPlaying)
			{
				BridgeLog.Info("剧情结束：期间已切到 " + Label(current.Id) + "，不恢复 " + Label(storyYieldWho) + "。");
			}
			else
			{
				Claim(storyYieldWho);
				IMusicModule musicModule = MusicModules.Of(storyYieldWho);
				if (!musicModule.IsPlaying)
				{
					musicModule.TogglePlayPause();
				}
				BridgeLog.Info("剧情结束：" + Label(storyYieldWho) + " 已恢复播放。");
			}
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("剧情恢复失败：" + ex.Message);
		}
	}

	public static void Claim(MusicProvider who)
	{
		if (_active != who)
		{
			_active = who;
			BridgeLog.Info("发声权 -> " + Label(who) + "，其余音源已暂停。");
			PauseAllExcept(who);
		}
	}

	public static void Relinquish(MusicProvider who, MusicProvider fallback)
	{
		if (_active == who)
		{
			BridgeLog.Info(Label(who) + " 交出发声权。");
			Claim(fallback);
		}
	}

	private static void PauseAllExcept(MusicProvider keep)
	{
		IMusicModule[] all = MusicModules.All;
		foreach (IMusicModule musicModule in all)
		{
			if (musicModule.Id != keep)
			{
				try
				{
					musicModule.PauseIfPlaying();
				}
				catch (Exception ex)
				{
					BridgeLog.Warn("暂停 " + Label(musicModule.Id) + " 失败：" + ex.Message);
				}
			}
		}
	}

	public static string Label(MusicProvider p)
	{
		return p switch
		{
			MusicProvider.Netease => "网易云",
			MusicProvider.AppleMusic => "Apple Music",
			_ => "本地音乐",
		};
	}
}
