namespace MusicBridge;

internal sealed class TrackInfo
{
	public long Id;

	public string Name = "";

	public string Artists = "";

	public string Album = "";

	public string Alias = "";

	public string ArtistAlias = "";

	public string CoverUrl = "";

	public int DurationMs;

	public bool Playable = true;

	public string UnplayableReason;

	public string DurationText
	{
		get
		{
			if (DurationMs <= 0)
			{
				return "";
			}
			int num = DurationMs / 1000;
			return num / 60 + ":" + (num % 60).ToString("00");
		}
	}
}
