// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Forked from Yeusepe's DiscordOSC (https://github.com/Yeusepe/Yeusepes-Modules, GPL-3.0).
// Incoming avatar parameters -> RPC commands.

using Bluscream.Modules.DiscordVoice.Rpc;
using VRCOSC.App.SDK.Parameters;

namespace Bluscream.Modules.DiscordVoice;

public sealed partial class DiscordVoiceModule
{
    protected override void OnRegisteredParameterReceived(RegisteredParameter parameter)
    {
        switch (parameter.Lookup)
        {
            case DiscordVoiceParameter.Mute:
            case DiscordVoiceParameter.Muted:
                SetSelfVoiceFlag(deafen: false, parameter.GetValue<bool>());
                break;
            case DiscordVoiceParameter.Deafen:
            case DiscordVoiceParameter.Deafened:
                SetSelfVoiceFlag(deafen: true, parameter.GetValue<bool>());
                break;
            case DiscordVoiceParameter.RequestGuildCount when parameter.GetValue<bool>():
                Send(Payload.GetGuilds());
                break;
            case DiscordVoiceParameter.RequestChannelCount when parameter.GetValue<bool>() && Wildcard(parameter, 0) is { } guildId:
                _lastGuildId = guildId;
                Send(Payload.GetChannels(guildId));
                break;
            case DiscordVoiceParameter.SelectVoiceChannel when parameter.GetValue<bool>() && Wildcard(parameter, 0) is { } channelId:
                Send(Payload.SelectVoiceChannel(channelId));
                break;
            case DiscordVoiceParameter.RequestSelectedVoiceChannel when parameter.GetValue<bool>():
                Send(Payload.GetSelectedVoiceChannel());
                break;
            case DiscordVoiceParameter.RequestVoiceSettings when parameter.GetValue<bool>():
                Send(Payload.GetVoiceSettings());
                break;
            case DiscordVoiceParameter.InputVolume:
            case DiscordVoiceParameter.SetInputVolume:
                Send(Payload.SetVoiceSettings(new Dictionary<string, object> { ["input"] = new { volume = parameter.GetValue<float>() } }));
                break;
            case DiscordVoiceParameter.OutputVolume:
            case DiscordVoiceParameter.SetOutputVolume:
                Send(Payload.SetVoiceSettings(new Dictionary<string, object> { ["output"] = new { volume = parameter.GetValue<float>() } }));
                break;
            case DiscordVoiceParameter.SetVoiceSettings:
                HandleSetVoiceSettings(parameter);
                break;
            case DiscordVoiceParameter.RequestChannelUserCount when parameter.GetValue<bool>() && Wildcard(parameter, 0) is { } channelId:
                Send(Payload.GetChannel(channelId));
                break;
            case DiscordVoiceParameter.RequestChannelType when parameter.GetValue<bool>() && Wildcard(parameter, 0) is { } channelId:
                Send(Payload.GetChannel(channelId));
                break;
            case DiscordVoiceParameter.SubscribeEvent when parameter.GetValue<bool>() && Wildcard(parameter, 0) is { } evt:
                Send(Payload.Subscribe(evt, SubscriptionArgs(evt, Wildcard(parameter, 1))));
                break;
            case DiscordVoiceParameter.UnsubscribeEvent when parameter.GetValue<bool>() && Wildcard(parameter, 0) is { } evt:
                Send(Payload.Unsubscribe(evt, SubscriptionArgs(evt, Wildcard(parameter, 1))));
                break;
            case DiscordVoiceParameter.SelectTextChannel when parameter.GetValue<bool>() && Wildcard(parameter, 0) is { } channelId:
                Send(Payload.SelectTextChannel(channelId));
                break;
            case DiscordVoiceParameter.SetUserVolume when Wildcard(parameter, 0) is { } userId:
                Send(Payload.SetUserVoiceSettings(userId, volume: (int)parameter.GetValue<float>()));
                break;
            case DiscordVoiceParameter.SetUserMute when Wildcard(parameter, 0) is { } userId:
                Send(Payload.SetUserVoiceSettings(userId, mute: parameter.GetValue<bool>()));
                break;
            case DiscordVoiceParameter.SetActivity when parameter.GetValue<bool>() && Wildcard(parameter, 0) is { } state && Wildcard(parameter, 1) is { } details:
                Send(Payload.SetActivity(state, details, string.Empty, string.Empty));
                break;
            case DiscordVoiceParameter.SendActivityJoinInvite when parameter.GetValue<bool>() && Wildcard(parameter, 0) is { } userId:
                Send(Payload.SendActivityJoinInvite(userId));
                break;
            case DiscordVoiceParameter.CloseActivityRequest when parameter.GetValue<bool>() && Wildcard(parameter, 0) is { } userId:
                Send(Payload.CloseActivityRequest(userId));
                break;
            case DiscordVoiceParameter.SendCertifiedDevices when parameter.GetValue<bool>():
                Send(Payload.SetCertifiedDevices(ExampleCertifiedDevices()));
                break;
            case DiscordVoiceParameter.RequestGuildInfo when parameter.GetValue<bool>() && Wildcard(parameter, 0) is { } guildId:
                Send(Payload.GetGuild(guildId));
                break;
        }
    }

    /// <summary>The string wildcard at <paramref name="position"/>, or null when absent or not a string.</summary>
    private static string? Wildcard(RegisteredParameter parameter, int position)
        => parameter.IsWildcardType<string>(position) ? parameter.GetWildcard<string>(position) : null;

    private static object? SubscriptionArgs(string evt, string? id)
    {
        if (id is null) return null;
        if (evt == "GUILD_STATUS") return new { guild_id = id };
        if (evt.StartsWith("VOICE_STATE", StringComparison.Ordinal) || evt.StartsWith("MESSAGE_", StringComparison.Ordinal) || evt is "SPEAKING_START" or "SPEAKING_STOP")
            return new { channel_id = id };
        return null;
    }

    /// <summary>VRCOSC/Discord/SetVoiceSettings/&lt;field&gt;/&lt;value&gt;: value may be a numeric wildcard or the bool payload.</summary>
    private void HandleSetVoiceSettings(RegisteredParameter parameter)
    {
        var field = Wildcard(parameter, 0)?.ToLowerInvariant();
        if (field is null) return;

        float? number = parameter.IsWildcardType<float>(1) ? parameter.GetWildcard<float>(1)
            : parameter.IsWildcardType<int>(1) ? parameter.GetWildcard<int>(1)
            : null;
        var flag = number.HasValue ? number.Value != 0 : parameter.GetValue<bool>();

        var args = new Dictionary<string, object>();
        switch (field)
        {
            case "mute": args["mute"] = flag; break;
            case "deaf": args["deaf"] = flag; break;
            case "input_volume" or "inputvolume" when number.HasValue: args["input"] = new { volume = number.Value }; break;
            case "output_volume" or "outputvolume" when number.HasValue: args["output"] = new { volume = number.Value }; break;
            case "automatic_gain_control" or "agc": args["automatic_gain_control"] = flag; break;
            case "echo_cancellation": args["echo_cancellation"] = flag; break;
            case "noise_suppression": args["noise_suppression"] = flag; break;
            case "qos": args["qos"] = flag; break;
            case "silence_warning": args["silence_warning"] = flag; break;
        }

        if (args.Count > 0) Send(Payload.SetVoiceSettings(args));
    }

    private static object[] ExampleCertifiedDevices() =>
    [
        new
        {
            type = "audioinput",
            id = Guid.NewGuid().ToString(),
            vendor = new { name = "Generic", url = "https://localhost" },
            model = new { name = "Example", url = "https://localhost" },
            related = Array.Empty<string>(),
            echo_cancellation = true,
            noise_suppression = true,
            automatic_gain_control = true,
            hardware_mute = false
        }
    ];
}
