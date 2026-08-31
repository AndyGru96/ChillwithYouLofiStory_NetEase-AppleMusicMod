using System;

namespace MusicBridge;

internal sealed class NeteaseOptions
{
	public TimeSpan QrPollInterval = TimeSpan.FromSeconds(2.0);

	public TimeSpan QrLifetime = TimeSpan.FromSeconds(240.0);

	public TimeSpan LoginSuccessCardLinger = TimeSpan.FromMilliseconds(1200.0);

	public int UserPlaylistPageSize = 100;

	public int UserPlaylistMaximumPageCount = 40;

	public int ServicePointConnectionLimit = 8;

	public int SongDetailBatchSize = 200;

	public int SearchPageSize = 30;

	public TimeSpan AudioRequestTimeout = TimeSpan.FromSeconds(45.0);

	public TimeSpan AudioStallTimeout = TimeSpan.FromSeconds(15.0);

	public long AudioCacheCapacityBytes = 536870912L;

	public long AudioCacheMaximumFileBytes = 67108864L;

	public long SessionMaximumFileBytes = 65536L;

	public bool RepeatQueue = true;
}
