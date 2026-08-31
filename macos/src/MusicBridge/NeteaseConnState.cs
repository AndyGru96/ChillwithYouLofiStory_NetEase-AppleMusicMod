namespace MusicBridge;

internal enum NeteaseConnState
{
	NotConnected,
	Restoring,
	Connected,
	NeedsReconnect,
	SessionCorrupted,
	NetworkUnavailable
}
