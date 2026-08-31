using System;
using System.Collections.Generic;

namespace MusicBridge;

// Main-thread policy: at most three resets in a minute, including delayed error logs.
internal sealed class AudioRecoveryPolicy
{
    public bool Failed { get; private set; }
    public bool Pending { get; private set; }
    public int Attempts => _attemptTimes.Count;
    public double DueAt { get; private set; }
    private readonly Queue<double> _attemptTimes = new Queue<double>();

    public static bool IsOutputFailure(string message)
    {
        if (string.IsNullOrEmpty(message)) return false;
        // A device-change warning precedes Unity's own retry, which may succeed.
        // Resetting on that warning can itself trigger another device-change warning.
        return message.StartsWith("FMOD failed to initialize any audio devices", StringComparison.Ordinal)
            || message.StartsWith("FMOD failed to initialize the output device, attempting to initialize the null output", StringComparison.Ordinal);
    }

    private void RefreshWindow(double now)
    {
        while (_attemptTimes.Count > 0 && now - _attemptTimes.Peek() >= 60) _attemptTimes.Dequeue();
    }

    public void ReportFailure(double now)
    {
        RefreshWindow(now);
        Failed = true;
        if (!Pending && Attempts < 3) { Pending = true; DueAt = now + (Attempts == 0 ? 2 : Attempts == 1 ? 5 : 10); }
    }

    public bool RequestRetry(double now)
    {
        RefreshWindow(now);
        if (Pending || Attempts >= 3) return false;
        Failed = true; Pending = true; DueAt = now + 0.5;
        return true;
    }

    public bool TryBegin(double now)
    {
        if (!Pending || now < DueAt) return false;
        RefreshWindow(now);
        if (Attempts >= 3) { Pending = false; return false; }
        _attemptTimes.Enqueue(now); Pending = false;
        return true;
    }

    public void Complete(bool success, double now)
    {
        if (success) { Failed = false; Pending = false; }
        else ReportFailure(now);
    }
}
