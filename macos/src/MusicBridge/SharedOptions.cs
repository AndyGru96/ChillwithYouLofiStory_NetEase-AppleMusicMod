using System;

namespace MusicBridge;

internal sealed class SharedOptions
{
	public TimeSpan HttpTimeout = TimeSpan.FromSeconds(15.0);

	public TimeSpan CoverDownloadTimeout = TimeSpan.FromSeconds(15.0);

	public int CoverMaximumConcurrentDownloads = 4;

	public int CoverMaximumEntries = 400;

	public long CoverMaximumDecodedBytes = 67108864L;

	public long LogMaximumFileBytes = 5242880L;

	public int LogRetainDays = 30;

	public bool PauseGameMusicUntilUserChooses = true;
}
