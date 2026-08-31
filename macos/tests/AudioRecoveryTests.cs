using System;
using MusicBridge;

internal static class AudioRecoveryTests
{
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
    }
    public static void Run()
    {
        Check(AudioRecoveryPolicy.IsOutputFailure("FMOD failed to initialize any audio devices, running on emulated software output with no sound.")
            && !AudioRecoveryPolicy.IsOutputFailure("Default audio device was changed, but the audio system failed to initialize it. Attempting to reset sound system.")
            && !AudioRecoveryPolicy.IsOutputFailure("FMOD failed to initialize the output device.: driver failed (57)")
            && AudioRecoveryPolicy.IsOutputFailure("FMOD failed to initialize the output device, attempting to initialize the null output.")
            && !AudioRecoveryPolicy.IsOutputFailure("MusicBridge: FMOD failed to initialize the output device")
            && !AudioRecoveryPolicy.IsOutputFailure("Audio download failed")
            && !AudioRecoveryPolicy.IsOutputFailure(null), "only terminal silent-output failure triggers recovery; transient retries do not create a reset loop");
        var p = new AudioRecoveryPolicy();
        Check(!p.Failed && !p.TryBegin(10), "normal playback does not trigger audio resets");
        p.ReportFailure(10); p.ReportFailure(11);
        Check(!p.TryBegin(11.9) && p.TryBegin(12), "device failure is debounced while the device settles");
        p.Complete(false, 12);
        Check(!p.TryBegin(16.9) && p.TryBegin(17), "failed reset retries with delay");
        p.Complete(false, 17);
        Check(!p.TryBegin(26.9) && p.TryBegin(27), "repeated failure increases retry delay");
        p.Complete(false, 27);
        Check(p.Failed && !p.Pending && !p.TryBegin(28) && !p.RequestRetry(28), "persistent failure stops after three attempts instead of resetting forever");
        Check(!p.RequestRetry(71) && p.RequestRetry(88) && p.TryBegin(89), "manual retry becomes available after cooldown");
        p.Complete(true,89);
        Check(!p.Failed && !p.Pending, "successful recovery clears fault status");
        // Some drivers report a failure after Reset returned true. These must still count.
        p.ReportFailure(90); Check(p.TryBegin(95), "late driver failure can schedule another bounded attempt");
        p.Complete(true,95); p.ReportFailure(96); Check(p.TryBegin(106), "late failures keep retry history");
        p.Complete(true,106); p.ReportFailure(107);
        Check(p.Failed && !p.Pending && !p.RequestRetry(108), "delayed errors cannot bypass the per-minute reset limit");
        var manual = new AudioRecoveryPolicy();
        Check(manual.RequestRetry(0) && !manual.RequestRetry(0.1) && !manual.TryBegin(0.4) && manual.TryBegin(0.5), "repeated repair button clicks are coalesced");
    }
}
