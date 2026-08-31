using UnityEngine.UI;

namespace MusicBridge;

internal sealed class GameBuiltInModule : IMusicModule
{
	public MusicProvider Id => MusicProvider.GameBuiltIn;

	public bool HasTrack
	{
		get
		{
			string title;
			string credit;
			float progress;
			return LocalMusicSource.TryGet(out title, out credit, out progress);
		}
	}

	public bool IsPlaying
	{
		get
		{
			if (HasTrack)
			{
				return GameNowPlayingBar.GameThinksItIsPlaying;
			}
			return false;
		}
	}

	public string Title
	{
		get
		{
			if (!LocalMusicSource.TryGet(out var title, out var _, out var _))
			{
				return null;
			}
			return title;
		}
	}

	public string Artist
	{
		get
		{
			if (!LocalMusicSource.TryGet(out var _, out var credit, out var _))
			{
				return null;
			}
			if (!string.IsNullOrEmpty(credit))
			{
				return credit;
			}
			return "游戏自带音乐";
		}
	}

	public double Position
	{
		get
		{
			if (!LocalMusicSource.TryGet(out var _, out var _, out var progress))
			{
				return 0.0;
			}
			return (double)progress * Duration;
		}
	}

	public double Duration => LocalMusicSource.PlayingDuration;

	public bool CanSeek => HasTrack;

	public bool SupportsLyrics => false;

	public bool Shuffle
	{
		get
		{
			return LocalMusicSource.IsShuffle;
		}
		set
		{
			if (value != LocalMusicSource.IsShuffle)
			{
				LocalMusicSource.InvokeFacility("OnClickButtonShuffleChange");
			}
		}
	}

	public bool RepeatOne
	{
		get
		{
			return LocalMusicSource.IsRepeatOne;
		}
		set
		{
			if (value != LocalMusicSource.IsRepeatOne)
			{
				LocalMusicSource.InvokeFacility("OnClickButtonChangeLoop");
			}
		}
	}

	public float Volume => GameNowPlayingBar.GameVolume;

	public string IdleHint => "在游戏播放列表里选一首歌开始播放";

	public string StatusPrefix
	{
		get
		{
			if (IsPlaying || !HasTrack || PlaybackCoordinator.IsSwitching)
			{
				return "";
			}
			return "已暂停 · ";
		}
	}

	public void ApplyCover(Image target)
	{
		if (!(target == null))
		{
			target.sprite = null;
			target.color = UiKit.CoverPlaceholder;
		}
	}

	public void TogglePlayPause()
	{
		LocalMusicSource.InvokeFacility("OnClickButtonPlayOrPauseMusic");
	}

	public void Next()
	{
		LocalMusicSource.InvokeFacility("OnClickButtonSkip");
	}

	public void Previous()
	{
		LocalMusicSource.InvokeFacility("OnClickButtonBack");
	}

	public void PauseIfPlaying()
	{
		LocalMusicSource.Pause();
	}

	public void SetVolume(float volume)
	{
		GameNowPlayingBar.SetGameVolume(volume);
	}

	public void Seek(double seconds)
	{
		double duration = Duration;
		if (duration > 0.0)
		{
			LocalMusicSource.SeekNormalized((float)(seconds / duration));
		}
	}
}
