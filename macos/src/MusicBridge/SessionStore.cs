using System;
namespace MusicBridge;
internal static class SessionStore
{
    public enum LoadResult { NotFound, Ok, Corrupted, Obsolete }
    public static bool Exists() { try { return MacKeychain.Load() != null; } catch { return false; } }
    public static LoadResult TryLoad(out string json)
    {
        json = null;
        try { json = MacKeychain.Load(); return json == null ? LoadResult.NotFound : LoadResult.Ok; }
        catch (Exception ex) { BridgeLog.Warn(ex.Message); return LoadResult.Corrupted; }
    }
    public static bool Save(string json)
    {
        try { MacKeychain.Save(json); BridgeLog.Info("网易云会话已保存到 macOS 钥匙串。"); return true; }
        catch (Exception ex) { BridgeLog.Warn(ex.Message); return false; }
    }
    public static bool Delete()
    {
        try { MacKeychain.Delete(); return true; }
        catch (Exception ex) { BridgeLog.Warn(ex.Message); return false; }
    }
}
