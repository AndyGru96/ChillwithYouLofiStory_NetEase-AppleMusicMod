using System.Collections.Generic;

namespace MusicBridge;

internal sealed class PlaylistInfo
{
	public long Id;

	public string Name = "";

	public string CreatorName = "";

	public long CreatorUserId;

	public int TrackCount;

	public string CoverUrl = "";

	public bool IsAlbum;

	public string AlbumType = "";

	public bool IsMine;

	public List<TrackInfo> Tracks = new List<TrackInfo>();

	public List<long> TrackIds;

	public bool TracksLoading;

	public bool TracksComplete;

	public string TracksError;

	public int MissingCount;

	public bool LoadAborted;

	public int LoadToken;

	public int LoadedCount => Tracks.Count;

	public string RowKey => PlaylistAssembly.RowKey(IsAlbum, Id);

	public void ResetTracks()
	{
		Tracks = new List<TrackInfo>();
		TrackIds = null;
		TracksLoading = false;
		TracksComplete = false;
		TracksError = null;
		MissingCount = 0;
		LoadAborted = false;
		LoadToken = 0;
	}
}
