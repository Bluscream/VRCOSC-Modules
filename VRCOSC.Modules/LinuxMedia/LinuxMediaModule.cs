// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Threading.Tasks;
using Bluscream.Modules.Utilities;
using VRCOSC.App.ChatBox.Clips.Variables.Instances;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;

namespace Bluscream.Modules;

[ModuleTitle("Linux Media")]
[ModuleDescription("Integration with Linux MPRIS Media Players (via D-Bus)")]
[ModuleType(ModuleType.Integrations)]
public class LinuxMediaModule : Module
{
    private string? _activePlayer;
    private string _playbackStatus = "Stopped";
    private string _title = "";
    private string _artist = "";
    private long _durationMicroseconds = 0;
    private long _positionMicroseconds = 0;
    private float _volume = 1f;

    protected override void OnPreLoad()
    {
        RegisterParameter<bool>(MediaParameter.Play, "VRCOSC/Media/Play", ParameterMode.ReadWrite, "Play/Pause", "True for playing. False for paused");
        RegisterParameter<bool>(MediaParameter.Next, "VRCOSC/Media/Next", ParameterMode.Read, "Next", "Becoming true causes the next track to play");
        RegisterParameter<bool>(MediaParameter.Previous, "VRCOSC/Media/Previous", ParameterMode.Read, "Previous", "Becoming true causes the previous track to play");
        RegisterParameter<float>(MediaParameter.Position, "VRCOSC/Media/Position", ParameterMode.ReadWrite, "Position", "The position of the song as a percentage");
        RegisterParameter<float>(MediaParameter.Volume, "VRCOSC/Media/Volume", ParameterMode.ReadWrite, "Volume", "The player volume as a percentage");
    }

    protected override void OnPostLoad()
    {
        var titleReference = CreateVariable<string>(MediaVariable.Title, "Title")!;
        var artistReference = CreateVariable<string>(MediaVariable.Artist, "Artist")!;
        CreateVariable<string>(MediaVariable.ArtistTitle, "Artist + Title");
        var currentTimeReference = CreateVariable<TimeSpan>(MediaVariable.Time, "Current Time")!;
        CreateVariable<TimeSpan>(MediaVariable.TimeRemaining, "Time Remaining");
        var durationReference = CreateVariable<TimeSpan>(MediaVariable.Duration, "Duration")!;
        var progressVisualReference = CreateVariable<float>(MediaVariable.ProgressVisual, "Progress Visual", typeof(ProgressClipVariable))!;
        CreateVariable<int>(MediaVariable.Volume, "Volume");

        CreateState(MediaState.Playing, "Playing", "[{0}/{1}]\n{2} - {3}\n{4}", new[] { currentTimeReference, durationReference, artistReference, titleReference, progressVisualReference });
        CreateState(MediaState.Paused, "Paused", "[Paused]\n{0} - {1}", new[] { artistReference, titleReference });
        CreateState(MediaState.Stopped, "Stopped", "[No Source]");

        CreateEvent(MediaEvent.OnTrackChange, "On Track Change", "Now Playing\n{0} - {1}", new[] { artistReference, titleReference }, true);
        CreateEvent(MediaEvent.OnPlay, "On Play", "[Playing]\n{0} - {1}", new[] { artistReference, titleReference });
        CreateEvent(MediaEvent.OnPause, "On Pause", "[Paused]\n{0} - {1}", new[] { artistReference, titleReference });
    }

    protected override Task<bool> OnModuleStart()
    {
        DeployHelperScript();
        ChangeState(MediaState.Stopped);
        return Task.FromResult(true);
    }

    private void DeployHelperScript()
    {
        try
        {
            string homeDir = Environment.GetEnvironmentVariable("HOME") ?? "/home/blu";
            string targetPath = System.IO.Path.Combine(homeDir, ".local", "bin", "vrcosc_mpris_query.sh");

            var assembly = typeof(LinuxMediaModule).Assembly;
            using var stream = assembly.GetManifestResourceStream("Bluscream.Modules.LinuxMedia.vrcosc_mpris_query.sh");
            if (stream == null)
            {
                Log("Error: Could not find embedded MPRIS query script resource.");
                return;
            }

            string wineHomeDir = "Z:" + homeDir.Replace('/', '\\');
            string wineTargetPath = System.IO.Path.Combine(wineHomeDir, ".local", "bin", "vrcosc_mpris_query.sh");

            string? dir = System.IO.Path.GetDirectoryName(wineTargetPath);
            if (dir != null && !System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }

            using (var fileStream = System.IO.File.Create(wineTargetPath))
            {
                stream.CopyTo(fileStream);
            }

            Log($"MPRIS query helper script deployed to {targetPath}");
            LinuxUtils.ChmodPlusX(targetPath, ex => Log($"Error making script executable: {ex.Message}"));
        }
        catch (Exception ex)
        {
            Log($"Error deploying MPRIS helper script: {ex.Message}");
        }
    }

    protected override Task OnModuleStop()
    {
        return Task.CompletedTask;
    }

    [ModuleUpdate(ModuleUpdateMode.Custom, true, 1000)]
    private void UpdateMediaState()
    {
        if (!Bluscream.ModuleUtils.IsStarted()) return;
        try
        {
            string homeDir = Environment.GetEnvironmentVariable("HOME") ?? "/home/blu";
            string wineHomeDir = "Z:" + homeDir.Replace('/', '\\');
            string tempFile = System.IO.Path.Combine(wineHomeDir, ".vrcosc_mpris.txt");
            string hostFile = homeDir + "/.vrcosc_mpris.txt";

            LinuxUtils.RunHost("/home/blu/.local/bin/vrcosc_mpris_query.sh", ex => Log($"Error: {ex.Message}"));

            if (!System.IO.File.Exists(tempFile))
            {
                return;
            }

            string output = System.IO.File.ReadAllText(tempFile).Trim();
            if (string.IsNullOrEmpty(output) || output == "Stopped")
            {
                _playbackStatus = "Stopped";
                _title = "";
                _artist = "";
                _durationMicroseconds = 0;
                _positionMicroseconds = 0;
                _activePlayer = null;
                ChangeState(MediaState.Stopped);
                return;
            }

            string[] lines = output.Split('\n');
            if (lines.Length >= 5)
            {
                var oldTitle = _title;
                var oldStatus = _playbackStatus;

                _playbackStatus = lines[0].Trim();
                _title = lines[1].Trim();
                _artist = lines[2].Trim();
                long.TryParse(lines[3].Trim(), out _durationMicroseconds);
                long.TryParse(lines[4].Trim(), out _positionMicroseconds);
                if (lines.Length >= 6) _activePlayer = lines[5].Trim();
                if (lines.Length >= 7) float.TryParse(lines[6].Trim(), out _volume);

                if (_title != oldTitle)
                {
                    Log($"Track changed: { (string.IsNullOrEmpty(_artist) ? _title : $"{_artist} - {_title}") }");
                    TriggerEvent(MediaEvent.OnTrackChange);
                }

                if (_playbackStatus != oldStatus)
                {
                    if (_playbackStatus == "Playing")
                    {
                        ChangeState(MediaState.Playing);
                        TriggerEvent(MediaEvent.OnPlay);
                    }
                    else if (_playbackStatus == "Paused")
                    {
                        ChangeState(MediaState.Paused);
                        TriggerEvent(MediaEvent.OnPause);
                    }
                    else
                    {
                        ChangeState(MediaState.Stopped);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Error querying MPRIS: {ex.Message}");
        }
    }

    [ModuleUpdate(ModuleUpdateMode.ChatBox)]
    private void UpdateChatBoxVariables()
    {
        var positionTime = TimeSpan.FromMilliseconds(_positionMicroseconds / 1000.0);
        var durationTime = TimeSpan.FromMilliseconds(_durationMicroseconds / 1000.0);
        var remainingTime = durationTime >= positionTime ? durationTime - positionTime : TimeSpan.Zero;
        float progress = _durationMicroseconds > 0 ? (float)_positionMicroseconds / _durationMicroseconds : 0f;

        SetVariableValue(MediaVariable.Title, _title);
        SetVariableValue(MediaVariable.Artist, _artist);
        SetVariableValue(MediaVariable.ArtistTitle, string.IsNullOrEmpty(_artist) ? _title : $"{_artist} - {_title}");
        SetVariableValue(MediaVariable.Time, positionTime);
        SetVariableValue(MediaVariable.Duration, durationTime);
        SetVariableValue(MediaVariable.TimeRemaining, remainingTime);
        SetVariableValue(MediaVariable.ProgressVisual, progress);
        SetVariableValue(MediaVariable.Volume, (int)Math.Round(_volume * 100));

        SendParameter(MediaParameter.Position, progress);
        SendParameter(MediaParameter.Play, _playbackStatus == "Playing");
        SendParameter(MediaParameter.Volume, _volume);
    }

    protected override void OnRegisteredParameterReceived(RegisteredParameter parameter)
    {
        if (string.IsNullOrEmpty(_activePlayer)) return;

        switch (parameter.Lookup)
        {
            case MediaParameter.Play:
                if (parameter.GetValue<bool>())
                    LinuxUtils.RunHost("/home/blu/.local/bin/vrcosc_mpris_query.sh control play",   ex => Log($"Error: {ex.Message}"));
                else
                    LinuxUtils.RunHost("/home/blu/.local/bin/vrcosc_mpris_query.sh control pause",  ex => Log($"Error: {ex.Message}"));
                break;

            case MediaParameter.Next when parameter.GetValue<bool>():
                LinuxUtils.RunHost("/home/blu/.local/bin/vrcosc_mpris_query.sh control next",       ex => Log($"Error: {ex.Message}"));
                break;

            case MediaParameter.Previous when parameter.GetValue<bool>():
                LinuxUtils.RunHost("/home/blu/.local/bin/vrcosc_mpris_query.sh control previous",   ex => Log($"Error: {ex.Message}"));
                break;

            case MediaParameter.Position:
                if (_durationMicroseconds > 0)
                {
                    long targetPos = (long)(parameter.GetValue<float>() * _durationMicroseconds);
                    LinuxUtils.RunHost($"/home/blu/.local/bin/vrcosc_mpris_query.sh control position {targetPos}", ex => Log($"Error: {ex.Message}"));
                }
                break;

            case MediaParameter.Volume:
                _volume = parameter.GetValue<float>();
                LinuxUtils.RunHost($"/home/blu/.local/bin/vrcosc_mpris_query.sh control volume {_volume}",          ex => Log($"Error: {ex.Message}"));
                break;
        }
    }



    private enum MediaParameter
    {
        Play,
        Next,
        Previous,
        Position,
        Volume
    }

    private enum MediaVariable
    {
        Title,
        Artist,
        ArtistTitle,
        Time,
        TimeRemaining,
        Duration,
        ProgressVisual,
        Volume
    }

    private enum MediaState
    {
        Playing,
        Paused,
        Stopped
    }

    private enum MediaEvent
    {
        OnTrackChange,
        OnPlay,
        OnPause
    }
}
