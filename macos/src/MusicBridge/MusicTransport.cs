namespace MusicBridge;

internal static class MusicTransport
{
	private static IMusicModule T => MusicModules.Current;

	public static void ClaimSelected()
	{
		BridgePanel.ClaimAudio(MusicModules.Selected.Id);
	}

	public static void TogglePlayPause()
	{
		PlaybackCoordinator.MarkUserChose();
		T.TogglePlayPause();
	}

	public static void Next()
	{
		PlaybackCoordinator.MarkUserChose();
		PlaybackCoordinator.NoteSwitching(2.5f);
		T.Next();
	}

	public static void Previous()
	{
		PlaybackCoordinator.MarkUserChose();
		PlaybackCoordinator.NoteSwitching(2.5f);
		T.Previous();
	}

	public static void SetVolume(float v)
	{
		T.SetVolume(v);
	}

	public static void SeekNormalized(float t)
	{
		IMusicModule t2 = T;
		if (t2.CanSeek)
		{
			double duration = t2.Duration;
			if (duration > 0.0)
			{
				t2.Seek((double)t * duration);
			}
		}
	}

	public static void ToggleShuffle()
	{
		IMusicModule t = T;
		t.Shuffle = !t.Shuffle;
		BridgeLog.Info("随机 -> " + BridgePanel.ProviderName(t.Id) + " = " + t.Shuffle);
	}

	public static void ToggleRepeatOne()
	{
		IMusicModule t = T;
		t.RepeatOne = !t.RepeatOne;
		BridgeLog.Info("单曲循环 -> " + BridgePanel.ProviderName(t.Id) + " = " + t.RepeatOne);
	}

	public static bool HandleGameButton(TransportAction action)
	{
		PlaybackCoordinator.MarkUserChose();
		if (action == TransportAction.PlayPause && MusicModules.Selected.Id == MusicProvider.GameBuiltIn && PlaybackCoordinator.Active != MusicProvider.GameBuiltIn)
		{
			ClaimSelected();
		}
		if (!T.HasTrack && action != TransportAction.Shuffle && action != TransportAction.RepeatOne)
		{
			return true;
		}
		switch (action)
		{
		case TransportAction.PlayPause:
			TogglePlayPause();
			break;
		case TransportAction.Next:
			Next();
			break;
		case TransportAction.Previous:
			Previous();
			break;
		case TransportAction.Shuffle:
			ToggleShuffle();
			break;
		case TransportAction.RepeatOne:
			ToggleRepeatOne();
			break;
		}
		return false;
	}
}
