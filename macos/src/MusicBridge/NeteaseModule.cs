using UnityEngine.UI;

namespace MusicBridge;

internal sealed class NeteaseModule : IMusicModule
{
	private static AudioPlayer P => AudioPlayer.Instance;

	public MusicProvider Id => MusicProvider.Netease;

	public bool HasTrack
	{
		get
		{
			if (P != null)
			{
				return P.IsActive;
			}
			return false;
		}
	}

	public bool IsPlaying
	{
		get
		{
			if (P != null)
			{
				return P.State == PlaybackState.Playing;
			}
			return false;
		}
	}

	public string Title
	{
		get
		{
			if (!(P != null) || P.CurrentTrack == null)
			{
				return null;
			}
			return P.CurrentTrack.Name;
		}
	}

	public string Artist
	{
		get
		{
			if (P == null || P.CurrentTrack == null)
			{
				return null;
			}
			TrackInfo currentTrack = P.CurrentTrack;
			string text = currentTrack.Artists;
			if (!string.IsNullOrEmpty(currentTrack.Album))
			{
				text = text + " · " + currentTrack.Album;
			}
			return text;
		}
	}

	public double Position => (P != null) ? P.PositionSeconds : 0f;

	public double Duration => (P != null) ? P.DurationSeconds : 0f;

	public bool CanSeek => HasTrack;

	public bool SupportsLyrics => true;

	public bool Shuffle
	{
		get
		{
			if (P != null)
			{
				return P.Shuffle;
			}
			return false;
		}
		set
		{
			if (P != null)
			{
				P.Shuffle = value;
			}
		}
	}

	public bool RepeatOne
	{
		get
		{
			if (P != null)
			{
				return P.RepeatOne;
			}
			return false;
		}
		set
		{
			if (P != null)
			{
				P.RepeatOne = value;
			}
		}
	}

	public float Volume
	{
		get
		{
			if (!(P != null))
			{
				return -1f;
			}
			return P.Volume;
		}
	}

	public string IdleHint => "在左侧歌单里选一首歌开始播放";

	public string StatusPrefix
	{
		get
		{
			if (P == null || P.CurrentTrack == null)
			{
				return "";
			}
			switch (P.State)
			{
			case PlaybackState.Loading:
				return "加载中 · ";
			case PlaybackState.Failed:
				return "无法播放 · ";
			case PlaybackState.Paused:
				if (!PlaybackCoordinator.IsSwitching)
				{
					return "已暂停 · ";
				}
				return "";
			default:
				return "";
			}
		}
	}

	public void ApplyCover(Image target)
	{
		if (!(target == null))
		{
			string text = ((P != null && P.CurrentTrack != null) ? P.CurrentTrack.CoverUrl : null);
			if (string.IsNullOrEmpty(text))
			{
				target.sprite = null;
				target.color = UiKit.CoverPlaceholder;
			}
			else
			{
				CoverCache.Apply(target, text, 120, UiKit.CoverPlaceholder);
			}
		}
	}

	public void TogglePlayPause()
	{
		if (P != null)
		{
			P.TogglePlayPause();
		}
	}

	public void Next()
	{
		if (P != null)
		{
			P.Next();
		}
	}

	public void Previous()
	{
		if (P != null)
		{
			P.Previous();
		}
	}

	public void PauseIfPlaying()
	{
		if (P != null)
		{
			P.PauseIfPlaying();
		}
	}

	public void SetVolume(float volume)
	{
		if (P != null)
		{
			P.Volume = volume;
		}
	}

	public void Seek(double seconds)
	{
		if (P != null)
		{
			P.Seek((float)seconds);
		}
	}
}
