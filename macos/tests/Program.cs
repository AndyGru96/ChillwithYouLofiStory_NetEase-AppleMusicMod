using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MusicBridge;

internal static class Program
{
    static void Assert(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); }
    static void Main(string[] args)
    {
        AudioRecoveryTests.Run();
        var tree = new List<AmPlaylist> { new AmPlaylist { Name = "测试 \"歌单\"\n🎵", PersistentId = "ABC", DeclaredCount = 1, TrackState = AmTrackState.Loaded, TracksComplete = true, ChildrenLoaded = true } };
        tree[0].Tracks.Add(new AmTrack { Name = "Track", PersistentId = "DEF", Artists = "艺术家", RowIndex = 0, DurationText = "3:05" });
        AmValidation validation;
        Assert(AppleMusicCache.Commit(tree, "test", out validation), "valid library commit");
        var loaded = AppleMusicCache.Load("test");
        Assert(loaded[0].Tracks[0].PersistentId == "DEF", "persistent track ID survives cache round trip");
        tree[0].DeclaredCount = 2;
        Assert(!AppleMusicCache.Commit(tree, "test", out validation), "incomplete library rejected");
        Assert(AppleMusicCache.Load("test")[0].Tracks.Count == 1, "previous cache retained after failure");
        Assert(AppleMusicCache.Validate(new List<AmPlaylist>()).Ok, "empty Music library is valid");
        bool rejected = false;
        try { BridgePaths.Resolve("..", "outside"); } catch (InvalidOperationException) { rejected = true; }
        Assert(rejected, "path traversal rejected");
        NeteaseCrypto.Encrypt("{}", out string encrypted, out string key);
        Assert(!string.IsNullOrEmpty(encrypted) && key.Length == 256, "NetEase AES/RSA encryption");
        if (args.Contains("--keychain")) {
            MacKeychain.Service = "com.chillwithyou.musicbridge.test." + Guid.NewGuid().ToString("N");
            try {
                Assert(MacKeychain.Load() == null, "isolated keychain item absent");
                MacKeychain.Save("test-only-中文");
                Assert(MacKeychain.Load() == "test-only-中文", "keychain UTF-8 round trip");
                MacKeychain.Save("updated");
                Assert(MacKeychain.Load() == "updated", "keychain update");
            } finally { MacKeychain.Delete(); }
            Assert(MacKeychain.Load() == null, "test keychain item deleted");
        }
        if (args.Contains("--music")) {
            MacMusicBackend.ScriptPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../scripts/music.js"));
            MacMusicBackend.Connect();
            Assert(MacMusicBackend.GetVolume() >= 0, "Music connection / volume read");
            Assert(MacMusicBackend.ReadSnapshot() != null, "Music playback snapshot read");
            var playlists = MacMusicBackend.Scan(_ => {}, () => false);
            Assert(AppleMusicCache.Validate(playlists).Ok, "real Music library structure and tracks validate");
            Console.WriteLine("Music root playlists: " + playlists.Count);
        }
        if (args.Contains("--network")) {
            var qrKey = NeteaseApi.RequestUniKey(out bool networkError);
            Assert(!networkError && !string.IsNullOrEmpty(qrKey), "live NetEase QR request (no login)");
            Assert(NeteaseApi.CheckQrStatus(qrKey) == QrStatus.WaitingScan, "live NetEase QR polling");
        }
    }
}
namespace MusicBridge {
    internal static class BridgeLog {
        public static void Info(string s) {} public static void Warn(string s) { Console.WriteLine("WARN: " + s); }
        public static void Error(string s) { Console.WriteLine("ERROR: " + s); } public static void History(string s) {}
        public static string Redact(string s) { return "[redacted]"; }
        public static void WarnThrottled(string k,string s,TimeSpan t) { Warn(s); }
    }
}
