namespace MusicBridge;

internal static class MusicModules
{
	public static readonly IMusicModule Game = new GameBuiltInModule();

	public static readonly IMusicModule Netease = new NeteaseModule();

	public static readonly IMusicModule Apple = new AppleModule();

	public static readonly IMusicModule[] All = new IMusicModule[3] { Game, Netease, Apple };

	public static IMusicModule Current => Of(PlaybackCoordinator.Active);

	public static IMusicModule Selected => Of(BridgePanel.CurrentProvider);

	public static IMusicModule Of(MusicProvider p)
	{
		return p switch
		{
			MusicProvider.GameBuiltIn => Game,
			MusicProvider.AppleMusic => Apple,
			_ => Netease,
		};
	}
}
