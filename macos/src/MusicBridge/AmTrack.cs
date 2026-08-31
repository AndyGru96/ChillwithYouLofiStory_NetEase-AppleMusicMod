namespace MusicBridge;

internal sealed class AmTrack
{
	public string PersistentId;

	public string Name = "";

	public string Artists = "";

	public string Album = "";

	public string DurationText = "";

	public int RowIndex;

	public override string ToString()
	{
		return Name + " · " + Artists;
	}
}
