using System.Collections.Generic;

namespace MusicBridge;

internal sealed class LocalLibrarySnapshot
{
	public const int CurrentSchemaVersion = 1;

	public int SchemaVersion = 1;

	public long Generation;

	public int NativeKeepCount = 100;

	public List<LocalTrackEntry> Tracks = new List<LocalTrackEntry>();

	public List<string> NativeProjection = new List<string>();

	public List<string> PlaylistOrder = new List<string>();

	public List<string> FavoriteAudioUUIDs = new List<string>();

	public List<string> ExcludedFromPlaylistUUIDs = new List<string>();

	public List<string> TrackIds()
	{
		List<string> list = new List<string>((Tracks != null) ? Tracks.Count : 0);
		if (Tracks == null)
		{
			return list;
		}
		foreach (LocalTrackEntry track in Tracks)
		{
			if (track != null && !string.IsNullOrEmpty(track.UUID))
			{
				list.Add(track.UUID);
			}
		}
		return list;
	}
}
