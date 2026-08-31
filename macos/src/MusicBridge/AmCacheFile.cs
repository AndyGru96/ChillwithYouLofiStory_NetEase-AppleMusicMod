using System.Collections.Generic;

namespace MusicBridge;

internal sealed class AmCacheFile
{
	public int Version = 3;

	public string Account;

	public string SavedAt;

	public string Fingerprint;

	public List<AmCachedNode> Nodes;
}
