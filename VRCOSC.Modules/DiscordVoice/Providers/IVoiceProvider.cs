// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// A fallback source of voice state for when Discord RPC is unavailable (no application
// credentials, Vesktop/Equibop's arRPC which has no voice commands, or a Wine prefix that
// cannot reach the native IPC socket). Each provider feeds its own VoiceStateTracker so the
// module can switch sources without the trackers contaminating each other.

using Bluscream.Modules.DiscordVoice.Voice;

namespace Bluscream.Modules.DiscordVoice.Providers;

public interface IVoiceProvider : IDisposable
{
    string Name { get; }

    /// <summary>True while the provider has a live link to the Discord client.</summary>
    bool IsAvailable { get; }

    VoiceStateTracker Tracker { get; }

    /// <summary>Starts listening / polling. Must not throw; failures are reported through <paramref name="log"/>.</summary>
    Task StartAsync(Action<string> log, CancellationToken ct);

    /// <summary>Asks the client to change our mute / deafen state. Returns false when the provider cannot.</summary>
    Task<bool> SetMuteAsync(bool mute, CancellationToken ct);
    Task<bool> SetDeafenAsync(bool deafen, CancellationToken ct);
}
