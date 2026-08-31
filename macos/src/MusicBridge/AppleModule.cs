using UnityEngine;
using UnityEngine.UI;

namespace MusicBridge;

internal sealed class AppleModule : IMusicModule
{
	public MusicProvider Id => MusicProvider.AppleMusic;

	public bool HasTrack => AppleMusicService.HasTrack;

	public bool IsPlaying => AppleMusicService.IsPlaying;

	public string Title
	{
		get
		{
			SmtcSnapshot nowPlaying = AppleMusicService.NowPlaying;
			if (nowPlaying == null || !nowPlaying.Valid)
			{
				return null;
			}
			return nowPlaying.Title;
		}
	}

	public string Artist
	{
		get
		{
			SmtcSnapshot nowPlaying = AppleMusicService.NowPlaying;
			if (nowPlaying == null || !nowPlaying.Valid)
			{
				return null;
			}
			string text = nowPlaying.Artist;
			if (!string.IsNullOrEmpty(nowPlaying.AlbumTitle))
			{
				text = text + " · " + nowPlaying.AlbumTitle;
			}
			return text;
		}
	}

	public double Position => AppleMusicService.GetPosition();

	public double Duration => AppleMusicService.Duration;

	public bool CanSeek => true;

	public bool SupportsLyrics => true;

	public bool Shuffle
	{
		get
		{
			return AppleMusicService.Shuffle;
		}
		set
		{
			AppleMusicService.Shuffle = value;
		}
	}

	public bool RepeatOne
	{
		get
		{
			return AppleMusicService.RepeatOne;
		}
		set
		{
			AppleMusicService.RepeatOne = value;
		}
	}

	public float Volume => AppleMusicService.Volume;

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

	public string IdleHint
	{
		get
		{
			object obj;
			if (AppleMusicService.ConnState != AmConnState.Connected)
			{
				obj = AppleMusicService.StatusText;
				if (obj == null)
				{
					return "未连接";
				}
			}
			else
			{
				obj = "在 Apple Music 里选一首歌开始播放";
			}
			return (string)obj;
		}
	}

	public void ApplyCover(Image target)
	{
		if (!(target == null))
		{
			Sprite coverSprite = AppleMusicService.CoverSprite;
			if (coverSprite == null)
			{
				target.sprite = null;
				target.color = UiKit.CoverPlaceholder;
			}
			else
			{
				target.sprite = coverSprite;
				target.color = Color.white;
			}
		}
	}

	public void TogglePlayPause()
	{
		AppleMusicService.TogglePlayPause();
	}

	public void Next()
	{
		AppleMusicService.NextInQueue();
	}

	public void Previous()
	{
		AppleMusicService.PreviousInQueue();
	}

	public void PauseIfPlaying()
	{
		AppleMusicService.PauseIfPlaying();
	}

	public void SetVolume(float volume)
	{
		AppleMusicService.SetVolume(volume);
	}

	public void Seek(double seconds)
	{
		AppleMusicService.Seek(seconds);
	}
}
