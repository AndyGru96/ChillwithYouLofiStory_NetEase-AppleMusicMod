namespace MusicBridge;

internal sealed class LocalTrack
{
	public int Index;

	public string Title = "";

	public string Credit = "";

	public double DurationSeconds;

	public string LocalPath = "";

	public object Raw;

	public bool IsImported;

	public string DurationText
	{
		get
		{
			if (DurationSeconds <= 0.0)
			{
				return "";
			}
			int num = (int)DurationSeconds;
			return num / 60 + ":" + (num % 60).ToString("00");
		}
	}
}
