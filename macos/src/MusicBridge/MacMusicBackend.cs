using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MusicBridge;

internal static class MacMusicBackend
{
    internal static string ScriptPath = Path.Combine(BridgePaths.Root, "music.js");

    internal static JToken Request(object request, int timeoutMs = 30000)
    {
        if (!File.Exists(ScriptPath)) throw new FileNotFoundException("缺少 music.js，请重新构建 macOS 包。");
        using (var process = new Process())
        {
            process.StartInfo = new ProcessStartInfo("/usr/bin/osascript", "-l JavaScript \"" + ScriptPath.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"") {
                UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true,
                RedirectStandardError = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            foreach (string key in new[] { "DYLD_INSERT_LIBRARIES", "DYLD_LIBRARY_PATH", "LD_PRELOAD", "DOORSTOP_ENABLED", "DOORSTOP_TARGET_ASSEMBLY" })
                process.StartInfo.EnvironmentVariables.Remove(key);
            process.Start();
            string output = null, error = null;
            var stdout = new Thread(() => { output = process.StandardOutput.ReadToEnd(); });
            var stderr = new Thread(() => { error = process.StandardError.ReadToEnd(); });
            stdout.IsBackground = stderr.IsBackground = true;
            stdout.Start(); stderr.Start();
            process.StandardInput.Write(JsonConvert.SerializeObject(request));
            process.StandardInput.Close();
            if (!process.WaitForExit(timeoutMs))
            {
                process.Kill(); process.WaitForExit(); stdout.Join(); stderr.Join();
                throw new TimeoutException("音乐 App 响应超时。请检查自动化授权弹窗，再重试。");
            }
            stdout.Join(); stderr.Join();
            if (process.ExitCode != 0)
            {
                if ((error ?? "").Contains("-1743"))
                    throw new InvalidOperationException("请在系统设置 → 隐私与安全性 → 自动化中允许启动此游戏的应用控制「音乐」。");
                throw new InvalidOperationException("音乐自动化失败：" + (error ?? "").Trim());
            }
            return JToken.Parse(output);
        }
    }

    public static void Connect() { Request(new { action = "connect" }); }
    public static SmtcSnapshot ReadSnapshot() { return Request(new { action = "snapshot" }).ToObject<SmtcSnapshot>(); }
    public static void Command(string action) { Request(new { action }); }
    public static void Seek(double seconds) { Request(new { action = "seek", seconds }); }
    public static void SetVolume(float volume) { Request(new { action = "setVolume", volume }); }
    public static float GetVolume() { return (float)Request(new { action = "volume" })["volume"]; }
    public static void Play(AmPlaylist playlist, AmTrack track)
    {
        if (string.IsNullOrEmpty(track.PersistentId)) throw new InvalidOperationException("请更新播放列表以读取 macOS 曲目 ID。");
        Request(new { action = "play", playlistId = playlist.PersistentId, trackId = track.PersistentId });
    }

    static void NormalizeOrder(List<AmPlaylist> nodes)
    {
        for (int i = 0; i < nodes.Count; i++) { nodes[i].Order = i; NormalizeOrder(nodes[i].Children); }
    }

    public static List<AmPlaylist> Scan(Action<string> progress, Func<bool> cancelled)
    {
        var nodes = Request(new { action = "playlists" }, 90000).ToObject<List<AmPlaylist>>();
        var byId = new Dictionary<string, AmPlaylist>();
        foreach (var p in nodes) byId.Add(p.PersistentId, p);
        var roots = new List<AmPlaylist>();
        foreach (var p in nodes)
        {
            if (cancelled()) throw new OperationCanceledException();
            p.ChildrenLoaded = true;
            p.AncestorIds = new List<string>();
            var visited = new HashSet<string> { p.PersistentId };
            var parentId = p.ParentId;
            while (!string.IsNullOrEmpty(parentId) && byId.ContainsKey(parentId))
            {
                if (!visited.Add(parentId)) throw new InvalidDataException("歌单文件夹存在循环。");
                p.AncestorIds.Insert(0, parentId);
                parentId = byId[parentId].ParentId;
            }
            p.Depth = p.AncestorIds.Count;
            AmPlaylist parent;
            if (p.ParentId != null && byId.TryGetValue(p.ParentId, out parent)) parent.Children.Add(p);
            else { p.ParentId = null; roots.Add(p); }
            if (p.IsFolder) continue;
            progress(p.Name);
            var tracks = Request(new { action = "tracks", playlistId = p.PersistentId }, 90000).ToObject<List<AmTrack>>();
            p.Tracks.AddRange(tracks);
            p.DeclaredCount = tracks.Count;
            p.TracksComplete = true;
            p.TrackState = tracks.Count == 0 ? AmTrackState.Empty : AmTrackState.Loaded;
        }
        NormalizeOrder(roots);
        return roots;
    }
}
