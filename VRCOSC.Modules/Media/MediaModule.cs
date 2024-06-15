﻿// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;
using VRCOSC.App.SDK.Providers.Media;

namespace VRCOSC.Modules.Media;

[ModuleTitle("Media")]
[ModuleDescription("Integration with Windows Media")]
[ModuleType(ModuleType.Integrations)]
[ModulePrefab("VRCOSC-Media", "https://github.com/VolcanicArts/VRCOSC/releases/download/latest/VRCOSC-Media.unitypackage")]
public class MediaModule : ChatBoxModule
{
    private const char progress_line = '\u2501';
    private const char progress_dot = '\u25CF';
    private const char progress_start = '\u2523';
    private const char progress_end = '\u252B';

    public WindowsMediaProvider MediaProvider { get; } = new();
    private bool currentlySeeking;
    private TimeSpan targetPosition;

    private MediaModuleRuntimePage runtimePage = null!;

    public MediaModule()
    {
        MediaProvider.OnPlaybackStateChange += onPlaybackStateChange;
        MediaProvider.OnTrackChange += onTrackChange;
        MediaProvider.OnLog += Log;
    }

    protected override void OnPreLoad()
    {
        CreateTextBox(MediaSetting.ProgressResolution, "Progress Resolution", "What resolution should the progress visual render at?", 10);

        RegisterParameter<bool>(MediaParameter.Play, "VRCOSC/Media/Play", ParameterMode.ReadWrite, "Play/Pause", "True for playing. False for paused");
        RegisterParameter<float>(MediaParameter.Volume, "VRCOSC/Media/Volume", ParameterMode.ReadWrite, "Volume", "The volume of the process that is controlling the media");
        RegisterParameter<int>(MediaParameter.Repeat, "VRCOSC/Media/Repeat", ParameterMode.ReadWrite, "Repeat", "0 - Disabled\n1 - Single\n2 - List");
        RegisterParameter<bool>(MediaParameter.Shuffle, "VRCOSC/Media/Shuffle", ParameterMode.ReadWrite, "Shuffle", "True for enabled. False for disabled");
        RegisterParameter<bool>(MediaParameter.Next, "VRCOSC/Media/Next", ParameterMode.Read, "Next", "Becoming true causes the next track to play");
        RegisterParameter<bool>(MediaParameter.Previous, "VRCOSC/Media/Previous", ParameterMode.Read, "Previous", "Becoming true causes the previous track to play");
        RegisterParameter<bool>(MediaParameter.Seeking, "VRCOSC/Media/Seeking", ParameterMode.Read, "Seeking", "Whether the user is currently seeking");
        RegisterParameter<float>(MediaParameter.Position, "VRCOSC/Media/Position", ParameterMode.ReadWrite, "Position", "The position of the song as a percentage");

        SetRuntimePage(runtimePage = new MediaModuleRuntimePage(this));
    }

    protected override void OnPostLoad()
    {
        var titleReference = CreateVariable<string>(MediaVariable.Title, "Title")!;
        var artistReference = CreateVariable<string>(MediaVariable.Artist, "Artist")!;
        CreateVariable<string>(MediaVariable.ArtistTitle, "Artist + Title");
        var currentTimeReference = CreateVariable<TimeSpan>(MediaVariable.Time, "Current Time")!;
        CreateVariable<TimeSpan>(MediaVariable.TimeRemaining, "Time Remaining");
        var durationReference = CreateVariable<TimeSpan>(MediaVariable.Duration, "Duration")!;
        CreateVariable<int>(MediaVariable.Volume, "Volume");
        CreateVariable<int>(MediaVariable.TrackNumber, "Track Number");
        CreateVariable<string>(MediaVariable.AlbumTitle, "Album Title");
        CreateVariable<string>(MediaVariable.AlbumArtist, "Album Artist");
        CreateVariable<int>(MediaVariable.AlbumTrackCount, "Album Track Count");
        var progressVisualReference = CreateVariable<string>(MediaVariable.ProgressVisual, "Progress Visual")!;

        CreateState(MediaState.Playing, "Playing", "[{0}/{1}]\n{2} - {3}\n{4}", new[] { currentTimeReference, durationReference, artistReference, titleReference, progressVisualReference });
        CreateState(MediaState.Paused, "Paused", "[Paused]\n{0} - {1}", new[] { artistReference, titleReference });
        CreateState(MediaState.Stopped, "Stopped", "[No Source]");

        CreateEvent(MediaEvent.OnTrackChange, "On Track Change", "Now Playing\n{0} - {1}", new[] { artistReference, titleReference }, true);
        CreateEvent(MediaEvent.OnPlay, "On Play", "[Playing]\n{0} - {1}", new[] { artistReference, titleReference });
        CreateEvent(MediaEvent.OnPause, "On Paused", "[Paused]\n{0} - {1}", new[] { artistReference, titleReference });
    }

    protected override async Task<bool> OnModuleStart()
    {
        var hookResult = await MediaProvider.InitialiseAsync();

        if (!hookResult)
        {
            Log("Could not hook into Windows media\nTry restarting the modules\nIf this persists you will need to restart your PC as Windows has not initialised media correctly");
            return false;
        }

        setState();

        return true;
    }

    protected override async Task OnModuleStop()
    {
        await MediaProvider.TerminateAsync();
    }

    protected override void OnAvatarChange()
    {
        sendUpdatableParameters();
        sendMediaParameters();
    }

    [ModuleUpdate(ModuleUpdateMode.Custom, true, 100)]
    private void fixedUpdate()
    {
        if (MediaProvider.State.IsPlaying)
        {
            // Hack to allow browsers to have time info
            MediaProvider.Update(TimeSpan.FromMilliseconds(100));
        }

        if (!currentlySeeking)
        {
            SendParameter(MediaParameter.Position, MediaProvider.State.Timeline.Progress);
        }
    }

    [ModuleUpdate(ModuleUpdateMode.ChatBox)]
    private void updateVariables()
    {
        SetVariableValue(MediaVariable.Title, MediaProvider.State.Title);
        SetVariableValue(MediaVariable.Artist, MediaProvider.State.Artist);
        SetVariableValue(MediaVariable.ArtistTitle, $"{MediaProvider.State.Artist} - {MediaProvider.State.Title}");
        SetVariableValue(MediaVariable.TrackNumber, MediaProvider.State.TrackNumber);
        SetVariableValue(MediaVariable.AlbumTitle, MediaProvider.State.AlbumTitle);
        SetVariableValue(MediaVariable.AlbumArtist, MediaProvider.State.AlbumArtist);
        SetVariableValue(MediaVariable.AlbumTrackCount, MediaProvider.State.AlbumTrackCount);
        SetVariableValue(MediaVariable.Volume, (int)MathF.Round(MediaProvider.TryGetVolume() * 100));
        SetVariableValue(MediaVariable.ProgressVisual, getProgressVisual());
        SetVariableValue(MediaVariable.Time, MediaProvider.State.Timeline.Position);
        SetVariableValue(MediaVariable.TimeRemaining, MediaProvider.State.Timeline.End - MediaProvider.State.Timeline.Position);
        SetVariableValue(MediaVariable.Duration, MediaProvider.State.Timeline.End);
    }

    private string getProgressVisual()
    {
        var progressResolution = GetSettingValue<int>(MediaSetting.ProgressResolution);

        var progressPercentage = progressResolution * MediaProvider.State.Timeline.Progress;
        var dotPosition = (int)(MathF.Floor(progressPercentage * 10f) / 10f);

        var visual = string.Empty;
        visual += progress_start;

        for (var i = 0; i < progressResolution; i++)
        {
            visual += i == dotPosition ? progress_dot : progress_line;
        }

        visual += progress_end;

        return visual;
    }

    [ModuleUpdate(ModuleUpdateMode.Custom, true, 1000)]
    private void sendUpdatableParameters()
    {
        SendParameter(MediaParameter.Volume, MediaProvider.TryGetVolume());
    }

    private void onPlaybackStateChange()
    {
        sendMediaParameters();
        setState();
    }

    private void setState()
    {
        if (MediaProvider.State.IsPaused)
        {
            ChangeState(MediaState.Paused);
            TriggerEvent(MediaEvent.OnPause);
        }

        if (MediaProvider.State.IsPlaying)
        {
            ChangeState(MediaState.Playing);
            TriggerEvent(MediaEvent.OnPlay);
        }

        if (MediaProvider.State.IsStopped)
        {
            ChangeState(MediaState.Stopped);
        }
    }

    private void onTrackChange()
    {
        TriggerEvent(MediaEvent.OnTrackChange);
    }

    private void sendMediaParameters()
    {
        SendParameter(MediaParameter.Play, MediaProvider.State.IsPlaying);
        SendParameter(MediaParameter.Shuffle, MediaProvider.State.IsShuffle);
        SendParameter(MediaParameter.Repeat, (int)MediaProvider.State.RepeatMode);
    }

    protected override void OnRegisteredParameterReceived(AvatarParameter parameter)
    {
        switch (parameter.Lookup)
        {
            case MediaParameter.Volume:
                MediaProvider.TryChangeVolume(parameter.GetValue<float>());
                break;

            case MediaParameter.Position:
                if (!currentlySeeking) return;

                var position = MediaProvider.State.Timeline;
                targetPosition = (position.End - position.Start) * parameter.GetValue<float>();
                break;

            case MediaParameter.Repeat:
                MediaProvider.ChangeRepeatMode((MediaRepeatMode)parameter.GetValue<int>());
                break;

            case MediaParameter.Play:
                if (parameter.GetValue<bool>())
                    MediaProvider.Play();
                else
                    MediaProvider.Pause();
                break;

            case MediaParameter.Shuffle:
                MediaProvider.ChangeShuffle(parameter.GetValue<bool>());
                break;

            case MediaParameter.Next when parameter.GetValue<bool>():
                MediaProvider.SkipNext();
                break;

            case MediaParameter.Previous when parameter.GetValue<bool>():
                MediaProvider.SkipPrevious();
                break;

            case MediaParameter.Seeking:
                currentlySeeking = parameter.GetValue<bool>();
                if (!currentlySeeking) MediaProvider.ChangePlaybackPosition(targetPosition);
                break;
        }
    }

    private enum MediaSetting
    {
        ProgressResolution
    }

    private enum MediaParameter
    {
        Play,
        Next,
        Previous,
        Shuffle,
        Repeat,
        Volume,
        Seeking,
        Position
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

    private enum MediaVariable
    {
        Title,
        Artist,
        ArtistTitle,
        Time,
        TimeRemaining,
        Duration,
        Volume,
        TrackNumber,
        AlbumTitle,
        AlbumArtist,
        AlbumTrackCount,
        ProgressVisual
    }
}
