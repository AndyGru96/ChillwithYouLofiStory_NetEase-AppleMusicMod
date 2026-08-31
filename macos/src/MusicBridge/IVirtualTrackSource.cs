namespace MusicBridge;

internal interface IVirtualTrackSource
{
	int Count { get; }

	long CurrentId { get; }

	long IdAt(int index);

	void Bind(PanelRows.TrackRow row, int index, bool isCurrent);

	void Activate(int index);
}
