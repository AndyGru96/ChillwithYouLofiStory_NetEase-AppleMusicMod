using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace Doorstop {
    public static class Entrypoint {
        static string Core => Path.GetDirectoryName(typeof(Entrypoint).Assembly.Location);
        static string Managed => Environment.GetEnvironmentVariable("CHILL_PROBE_MANAGED");
        static string Log => Environment.GetEnvironmentVariable("CHILL_PROBE_LOG");
        public static void Start() {
            int code = 1;
            try {
                AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => {
                    foreach (var dir in new[] { Core, Managed }) {
                        var path = Path.Combine(dir, new AssemblyName(args.Name).Name + ".dll");
                        if (File.Exists(path)) return Assembly.LoadFrom(path);
                    }
                    return null;
                };
                File.WriteAllText(Log, "Doorstop entered; pointer bytes=" + IntPtr.Size + "\n");
                Run(); code = 0;
            } catch (Exception ex) { File.AppendAllText(Log, ex.ToString()); }
            finally { Environment.Exit(code); } // Never run scenes, read/write saves, or initialize Steam.
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Run() {
            var preloader = Assembly.LoadFrom(Path.Combine(Core, "BepInEx.Preloader.dll"));
            preloader.GetType("Doorstop.Entrypoint").GetMethod("Start", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            File.AppendAllText(Log, "BepInEx preloader returned\n");
            var harmony = new Harmony("com.chillwithyou.musicbridge.smoketest");
            var method = typeof(Entrypoint).GetMethod("Original", BindingFlags.Public | BindingFlags.Static);
            harmony.Patch(method, postfix: new HarmonyMethod(typeof(Entrypoint).GetMethod("Postfix")));
            if ((int)method.Invoke(null, null) != 42) throw new Exception("Harmony patch failed");
            File.AppendAllText(Log, "Harmony patch executed successfully\n");
            var pluginPath = Path.GetFullPath(Path.Combine(Core, "../plugins/ChillWithYouMusicBridge/MusicBridge.Plugin.dll"));
            var plugin = Assembly.LoadFrom(pluginPath);
            var backend = plugin.GetType("MusicBridge.MacMusicBackend");
            backend.GetMethod("Connect").Invoke(null, null);
            if (backend.GetMethod("ReadSnapshot").Invoke(null, null) == null) throw new Exception("Music snapshot missing");
            File.AppendAllText(Log, "macOS Music automation works under game Mono\n");
            var keychain = plugin.GetType("MusicBridge.MacKeychain");
            keychain.GetField("Service", BindingFlags.Static|BindingFlags.NonPublic).SetValue(null, "com.chillwithyou.musicbridge.probe." + Guid.NewGuid().ToString("N"));
            try {
                keychain.GetMethod("Save").Invoke(null, new object[] { "probe-test-only" });
                if ((string)keychain.GetMethod("Load").Invoke(null, null) != "probe-test-only") throw new Exception("Keychain round trip failed");
            } finally { keychain.GetMethod("Delete").Invoke(null, null); }
            File.AppendAllText(Log, "macOS Keychain works under game Mono; temporary item deleted\n");
            var api = plugin.GetType("MusicBridge.NeteaseApi");
            object[] qrArgs = new object[] { false };
            string qrKey = (string)api.GetMethod("RequestUniKey").Invoke(null, qrArgs);
            if ((bool)qrArgs[0] || string.IsNullOrEmpty(qrKey)) throw new Exception("NetEase QR request failed under Mono");
            string qrStatus = api.GetMethod("CheckQrStatus").Invoke(null, new object[] { qrKey }).ToString();
            if (qrStatus != "WaitingScan") throw new Exception("NetEase QR polling: " + qrStatus);
            File.AppendAllText(Log, "NetEase QR request and polling work under game Mono (no login)\n");
            var game = Assembly.LoadFrom(Path.Combine(Managed, "Assembly-CSharp.dll"));
            var checks = new[] { "Bulbul.MusicPlayListView:Setup", "Bulbul.MusicUI:ActivatePlayList", "Bulbul.FacilityMusic:OnClickButtonPlayOrPauseMusic" };
            foreach (var check in checks) {
                var bits = check.Split(':');
                var type = game.GetType(bits[0]) ?? game.GetType(bits[0].Replace("Bulbul.", ""));
                if (type == null || type.GetMethod(bits[1], BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static) == null)
                    throw new Exception("Missing game hook " + check);
                File.AppendAllText(Log, "Game hook exists: " + check + "\n");
            }
        }
        [MethodImpl(MethodImplOptions.NoInlining)] public static int Original() { return 1; }
        public static void Postfix(ref int __result) { __result = 42; }
    }
}
