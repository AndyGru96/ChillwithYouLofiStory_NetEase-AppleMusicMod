namespace MusicBridge;

internal enum QrCardState
{
	Hidden,
	Creating,
	WaitingScan,
	ScannedWaitingConfirm,
	Success,
	Expired,
	NetworkError,
	Failed
}
