using System;

namespace MusicBridge;

internal sealed class SmtcSnapshot
{
	public bool Valid;

	public string AppId = "";

	public string Title = "";

	public string Artist = "";

	public string AlbumTitle = "";

	public string AlbumArtist = "";

	public int Status;

	public double PositionSeconds;

	public double DurationSeconds;

	public bool CanPause;

	public bool CanNext;

	public bool CanPrev;

	public bool CanSeek;

	public bool IsPlaying => Status == 4;

	public bool IsAppleMusic => AppId.IndexOf("AppleMusic", StringComparison.OrdinalIgnoreCase) >= 0;
}
