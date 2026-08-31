using System.Collections.Generic;

namespace MusicBridge;

internal sealed class AmNameSet
{
	public long TrackId;

	public double Seconds;

	public readonly List<string> Titles = new List<string>();

	public readonly List<string> Artists = new List<string>();

	public readonly List<string> Albums = new List<string>();

	public readonly List<AmLocalizedName> LocalizedNames = new List<AmLocalizedName>();

	public bool IsEmpty
	{
		get
		{
			if (Titles.Count == 0)
			{
				return Artists.Count == 0;
			}
			return false;
		}
	}

	public override string ToString()
	{
		return "曲名[" + string.Join("|", Titles.ToArray()) + "] 歌手[" + string.Join("|", Artists.ToArray()) + "] 专辑[" + string.Join("|", Albums.ToArray()) + "]";
	}
}
