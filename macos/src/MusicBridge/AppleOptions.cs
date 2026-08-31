using System;

namespace MusicBridge;

internal sealed class AppleOptions
{
	public TimeSpan VisibleStatusPollInterval = TimeSpan.FromSeconds(2.0);

	public TimeSpan QueueStatusPollInterval = TimeSpan.FromSeconds(2.0);

	public TimeSpan QueueTransitionGuard = TimeSpan.FromSeconds(10.0);

	public TimeSpan LyricsRetryDelay = TimeSpan.FromSeconds(10.0);

	public TimeSpan PendingCacheMaximumAge = TimeSpan.FromHours(2.0);

	public int ItemContainerMaximumItems = 5000;

	public TimeSpan MetadataDurationTolerance = TimeSpan.FromSeconds(3.0);

	public TimeSpan WorkerIdlePollInterval = TimeSpan.FromMilliseconds(40.0);

	public TimeSpan KeyChordStepDelay = TimeSpan.FromMilliseconds(40.0);

	public TimeSpan KeyChordHoldDelay = TimeSpan.FromMilliseconds(60.0);

	public TimeSpan PaneOpenSettleDelay = TimeSpan.FromMilliseconds(1400.0);

	public TimeSpan InitialPlaylistRootExpandDelay = TimeSpan.FromMilliseconds(800.0);

	public TimeSpan StandardPlaylistRootExpandDelay = TimeSpan.FromMilliseconds(900.0);

	public TimeSpan EmptyLibraryRetryDelay = TimeSpan.FromSeconds(6.0);

	public int EmptyLibraryMaximumRetryCount = 4;

	public TimeSpan StabilityVerificationInitialDelay = TimeSpan.FromMilliseconds(900.0);

	public TimeSpan StabilityVerificationIncrementDelay = TimeSpan.FromMilliseconds(500.0);

	public int StabilityVerificationCount = 2;

	public TimeSpan FolderExpandDelay = TimeSpan.FromMilliseconds(700.0);

	public TimeSpan StructureRescanRootExpandDelay = TimeSpan.FromMilliseconds(600.0);

	public TimeSpan StructureRescanFolderExpandDelay = TimeSpan.FromMilliseconds(450.0);

	public TimeSpan SidebarScrollStepDelay = TimeSpan.FromMilliseconds(120.0);

	public TimeSpan AncestorRootExpandDelay = TimeSpan.FromMilliseconds(500.0);

	public TimeSpan AncestorFolderExpandDelay = TimeSpan.FromMilliseconds(400.0);

	public TimeSpan ItemRealizeDelay = TimeSpan.FromMilliseconds(120.0);

	public TimeSpan SelectionConfirmationDelay = TimeSpan.FromMilliseconds(150.0);

	public TimeSpan PageReadyPollInterval = TimeSpan.FromMilliseconds(150.0);

	public int PageReadyMaximumPollCount = 20;

	public TimeSpan EnumerationRetryDelay = TimeSpan.FromMilliseconds(500.0);

	public TimeSpan TrackParseAfterScrollDelay = TimeSpan.FromMilliseconds(60.0);

	public TimeSpan PlaylistNavigationSettleDelay = TimeSpan.FromMilliseconds(600.0);

	public TimeSpan PointPlayNavigationSettleDelay = TimeSpan.FromMilliseconds(700.0);

	public TimeSpan SelectedRowSettleDelay = TimeSpan.FromMilliseconds(400.0);

	public TimeSpan QueueStopSettleDelay = TimeSpan.FromMilliseconds(900.0);

	public TimeSpan ToggleStateSettleDelay = TimeSpan.FromMilliseconds(400.0);

	public TimeSpan PlayVerificationRetryDelay = TimeSpan.FromMilliseconds(500.0);
}
