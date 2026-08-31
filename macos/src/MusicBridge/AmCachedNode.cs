using System.Collections.Generic;

namespace MusicBridge;

internal sealed class AmCachedNode
{
	public string Name;

	public string Id;

	public string ParentId;

	public int Order;

	public bool IsFolder;

	public int DeclaredCount = -1;

	public string Summary;

	public string TrackState;

	public List<AmCachedNode> Children;

	public List<AmCachedTrack> Tracks;
}
