namespace MusicBridge;

internal sealed class LocalOptions
{
	public bool UnlimitedImport = true;

	public bool VirtualizeNativeList = true;

	public int VirtualizeThreshold = 120;

	public bool ShowImportIndex = true;

	public bool UnloadUnusedAudio = true;

	public int LoadedClipBudget = 12;

	public int LoadedClipBudgetMegabytes = 512;

	public bool DeferStartupAudioLoad = true;

	public bool DeferImportAudioLoad = true;
}
