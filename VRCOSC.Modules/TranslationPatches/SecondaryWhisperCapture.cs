// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Second Whisper transcription on a capture device of the user's choice (a PipeWire/Pulse
// "Monitor of ..." source, which Wine exposes as an ordinary capture endpoint, or a virtual
// cable), used for the two-way variables. Reuses the app's own AudioProcessor, the class
// behind the built-in speech engine, so the model, GPU, noise cutoff and confidence settings
// are shared. AudioProcessor is internal to VRCOSC.App and NAudio is not referenced here, so
// everything is resolved by name at runtime and a missing member simply disables the feature.

using System.Reflection;
using VRCOSC.App.Audio;

namespace Bluscream.Modules.TranslationPatches;

internal sealed class SecondaryWhisperCapture
{
    private const string AudioProcessorTypeName = "VRCOSC.App.Audio.Whisper.AudioProcessor";
    private const string DeviceHelperTypeName = "VRCOSC.App.Audio.AudioDeviceHelper";

    private readonly Action<string> _log;
    private object? _processor;
    private MethodInfo? _getResult;
    private MethodInfo? _stop;
    private bool _busy;

    public bool IsRunning => _processor is not null;

    public SecondaryWhisperCapture(Action<string> log)
    {
        _log = log;
    }

    /// <summary>Lists active capture endpoints as "friendly name" strings, for log output and matching.</summary>
    public static IReadOnlyList<(string Name, object Device)> CaptureDevices()
    {
        var helper = typeof(SpeechEngine).Assembly.GetType(DeviceHelperTypeName, false);
        var list = helper?.GetMethod("GetAllInputDevices", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, null) as System.Collections.IEnumerable;
        if (list is null) return Array.Empty<(string, object)>();

        var devices = new List<(string, object)>();
        foreach (var device in list)
        {
            var name = device.GetType().GetProperty("FriendlyName")?.GetValue(device) as string ?? string.Empty;
            devices.Add((name, device));
        }

        return devices;
    }

    /// <summary>Starts transcribing the first capture device whose name contains <paramref name="deviceFilter"/>.</summary>
    public bool Start(string deviceFilter)
    {
        Stop();

        var processorType = typeof(SpeechEngine).Assembly.GetType(AudioProcessorTypeName, false);
        var ctor = processorType?.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(c => c.GetParameters().Length == 1);
        var start = processorType?.GetMethod("Start", BindingFlags.Instance | BindingFlags.Public);
        _getResult = processorType?.GetMethod("GetResultAsync", BindingFlags.Instance | BindingFlags.Public);
        _stop = processorType?.GetMethod("Stop", BindingFlags.Instance | BindingFlags.Public);

        if (ctor is null || start is null || _getResult is null || _stop is null)
        {
            _log("Two-way: the app's AudioProcessor does not have the expected shape in this VRCOSC version; two-way translation disabled.");
            return false;
        }

        var devices = CaptureDevices();
        var match = devices.FirstOrDefault(d => d.Name.Contains(deviceFilter, StringComparison.OrdinalIgnoreCase));
        if (match.Device is null)
        {
            _log($"Two-way: no capture device matches \"{deviceFilter}\". Available: {string.Join(" | ", devices.Select(d => d.Name))}");
            return false;
        }

        try
        {
            _processor = ctor.Invoke(new[] { match.Device });
            start.Invoke(_processor, null);
            _log($"Two-way: transcribing \"{match.Name}\" with a second Whisper instance.");
            return true;
        }
        catch (TargetInvocationException ex)
        {
            _log($"Two-way: could not start the capture: {ex.InnerException?.Message ?? ex.Message}");
            _processor = null;
            return false;
        }
    }

    /// <summary>One transcription step; null when nothing new, busy, or not running.</summary>
    public async Task<SpeechResult?> PollAsync()
    {
        if (_processor is null || _getResult is null || _busy) return null;

        _busy = true;
        try
        {
            if (_getResult.Invoke(_processor, null) is Task<SpeechResult?> task) return await task.ConfigureAwait(false);
            return null;
        }
        catch (TargetInvocationException ex)
        {
            _log($"Two-way: transcription step failed: {ex.InnerException?.Message ?? ex.Message}");
            return null;
        }
        finally { _busy = false; }
    }

    public void Stop()
    {
        if (_processor is null) return;

        try
        {
            if (_stop?.Invoke(_processor, null) is Task task) task.ContinueWith(t => _log($"Two-way: stop failed: {t.Exception?.GetBaseException().Message}"), TaskContinuationOptions.OnlyOnFaulted);
        }
        catch (TargetInvocationException ex)
        {
            _log($"Two-way: stop failed: {ex.InnerException?.Message ?? ex.Message}");
        }

        _processor = null;
    }
}
