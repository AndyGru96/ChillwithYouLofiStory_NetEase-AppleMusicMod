namespace MusicBridge;

internal enum QrStatus
{
	WaitingScan = 801,
	ScannedWaitingConfirm = 802,
	Success = 803,
	Expired = 800,
	NetworkError = -1,
	ProtocolError = -2
}
