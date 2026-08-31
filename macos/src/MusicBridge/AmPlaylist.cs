using System.Collections.Generic;

namespace MusicBridge;

internal sealed class AmPlaylist
{
	public string Name;

	public string PersistentId;

	public bool IsFolder;

	public readonly List<AmPlaylist> Children = new List<AmPlaylist>();

	public bool ChildrenLoaded;

	public int Depth;

	public int Order;

	public string ParentId;

	public List<string> AncestorIds;

	public int DeclaredCount = -1;

	public AmTrackState TrackState;

	public bool Expanded;

	public readonly List<AmTrack> Tracks = new List<AmTrack>();

	public bool TracksLoading;

	public bool TracksComplete;

	public string TracksError;

	public string Summary;

	public override string ToString()
	{
		return Name + "  <" + PersistentId + ">";
	}

	public void ResetTracks()
	{
		Tracks.Clear();
		TracksLoading = false;
		TracksComplete = false;
		TracksError = null;
	}
}
