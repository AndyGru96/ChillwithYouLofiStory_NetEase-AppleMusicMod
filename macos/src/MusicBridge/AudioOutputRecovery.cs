using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace MusicBridge;

// All Unity API calls stay on the main thread. Native logging only sets a signal.
internal sealed class AudioOutputRecovery : MonoBehaviour
{
    private sealed class SourceState
    {
        public AudioSource Source;
        public AudioClip Clip;
        public float Position;
    }

    private static AudioOutputRecovery _instance;
    private readonly AudioRecoveryPolicy _policy = new AudioRecoveryPolicy();
    private int _failureSignals;
    private int _deviceSignals;
    private bool _quitting;
    private bool _resetting;
    private bool _warnedExhausted;

    public static bool OutputUnavailable => _instance != null && _instance._policy.Failed;

    public static void Initialize()
    {
        if (_instance != null) return;
        var host = new GameObject("MusicBridge_AudioRecovery");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.hideFlags = HideFlags.HideAndDontSave;
        _instance = host.AddComponent<AudioOutputRecovery>();
    }

    private void Awake()
    {
        Application.logMessageReceivedThreaded += OnLog;
        AudioSettings.OnAudioConfigurationChanged += OnConfigurationChanged;
        BridgeLog.Info("音频输出故障检测已启用；仅在设备初始化失败或手动请求时恢复。");
    }

    private void OnLog(string message, string stack, LogType type)
    {
        if (AudioRecoveryPolicy.IsOutputFailure(message)) Interlocked.Increment(ref _failureSignals);
    }

    private void OnConfigurationChanged(bool deviceChanged)
    {
        if (deviceChanged) Interlocked.Exchange(ref _deviceSignals, 1);
    }

    public static void RequestManual()
    {
        if (_instance == null || _instance._quitting || _instance._resetting) return;
        if (!_instance._policy.RequestRetry(Time.realtimeSinceStartup))
            BridgeLog.Warn("音频恢复正在等待，或已达到一分钟三次的限制；请稍后再试。");
        else BridgeLog.Info("已请求恢复游戏音频输出，不修改系统音量或输出设备。");
    }

    private void Update()
    {
        if (_quitting || _resetting) return;
        float now = Time.realtimeSinceStartup;
        if (Interlocked.Exchange(ref _failureSignals, 0) != 0)
        {
            _policy.ReportFailure(now);
            BridgeLog.Warn("检测到 Unity 音频输出初始化失败；将限次尝试重新连接当前输出设备。");
        }
        if (Interlocked.Exchange(ref _deviceSignals, 0) != 0)
        {
            BridgeLog.Info("系统音频设备已变化。");
            if (_policy.Failed) _policy.RequestRetry(now);
        }
        if (_policy.TryBegin(now)) Recover();
        if (_policy.Failed && !_policy.Pending && _policy.Attempts >= 3 && !_warnedExhausted)
        {
            _warnedExhausted = true;
            BridgeLog.Warn("音频输出仍不可用，已停止自动重试。请确认系统输出设备，一分钟后点击“修复游戏声音”，或正常重启游戏。");
        }
    }

    private void Recover()
    {
        _resetting = true;
        bool success = false;
        var playing = new List<SourceState>();
        Action restoreMusic = null;
        try
        {
            var player = AudioPlayer.Instance;
            restoreMusic = player != null ? player.CaptureOutputRecovery() : null;
            // No per-frame scene scan. Remember only sources playing at the reset.
            foreach (var source in UnityEngine.Object.FindObjectsOfType<AudioSource>())
            {
                if (source == null || !source.isPlaying || source.clip == null || (player != null && player.Owns(source))) continue;
                playing.Add(new SourceState { Source = source, Clip = source.clip, Position = source.time });
            }
            var config = AudioSettings.GetConfiguration();
            BridgeLog.Info("尝试恢复游戏音频（本分钟第 " + _policy.Attempts + " 次）；保持现有音频配置与音量。");
            success = AudioSettings.Reset(config);
            // FMOD may return true after silently selecting its null output.
            if (Interlocked.Exchange(ref _failureSignals, 0) != 0) success = false;
            foreach (var state in playing)
            {
                var source = state.Source;
                if (source == null || state.Clip == null || source.clip != state.Clip || !source.isActiveAndEnabled || source.isPlaying) continue;
                try
                {
                    source.time = Mathf.Clamp(state.Position, 0, Mathf.Max(0, state.Clip.length - 0.05f));
                    source.Play();
                }
                catch (Exception ex) { BridgeLog.Warn("恢复游戏音源失败：" + ex.Message); }
            }
            // Re-decode our runtime clip even on failure: Reset can invalidate it.
            restoreMusic?.Invoke();
        }
        catch (Exception ex) { success = false; BridgeLog.Warn("恢复音频输出失败：" + ex.Message); }
        finally
        {
            _resetting = false;
            _policy.Complete(success, Time.realtimeSinceStartup);
        }
        if (success)
        {
            _warnedExhausted = false;
            BridgeLog.Info("Unity 已接受音频输出恢复；实际声音仍以输出设备为准。");
        }
    }

    private void OnApplicationQuit() { _quitting = true; }
    private void OnDestroy()
    {
        Application.logMessageReceivedThreaded -= OnLog;
        AudioSettings.OnAudioConfigurationChanged -= OnConfigurationChanged;
        if (_instance == this) _instance = null;
    }
}
