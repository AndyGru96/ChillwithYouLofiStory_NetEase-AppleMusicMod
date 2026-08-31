using System;

namespace MusicBridge;

internal sealed class LyricsOptions
{
	public int MaximumCrossLanguageQueries = 4;

	public TimeSpan ExactDurationTolerance = TimeSpan.FromSeconds(3.0);

	public TimeSpan StrongDurationTolerance = TimeSpan.FromSeconds(1.5);

	public TimeSpan TimestampMergeTolerance = TimeSpan.FromMilliseconds(30.0);

	public double PositionQuantizationCenterSeconds = 0.5;

	public TimeSpan ITunesRequestMinimumInterval = TimeSpan.FromMilliseconds(3100.0);

	public TimeSpan ITunesRetryBaseDelay = TimeSpan.FromSeconds(1.0);

	public TimeSpan ITunesRequestTimeout = TimeSpan.FromSeconds(8.0);

	public int ITunesMaximumRetryCount = 2;

	public int ITunesPersistentCacheMaximumEntries = 2000;
}
