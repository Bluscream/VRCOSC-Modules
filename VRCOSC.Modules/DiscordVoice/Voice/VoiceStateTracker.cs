// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Voice-channel bookkeeping shared by every source of voice state (RPC and the fallback
// providers): who is in the channel, who is speaking, and our own mute/deafen flags.
// Thread-safe; events arrive on transport threads while Snapshot() runs on the update tick.

namespace Bluscream.Modules.DiscordVoice.Voice;

/// <summary>What the ChatBox variables are rendered from.</summary>
public readonly record struct VoiceSnapshot(string ChannelName, string ChannelId, int UserCount, IReadOnlyList<string> Speaking, bool Muted, bool Deafened)
{
    public bool InVoice => ChannelId.Length > 0;

    /// <summary>"" / "muted" / "deafened", the way MagicChatbox renders it. Deafened implies muted.</summary>
    public string MuteState => Deafened ? "deafened" : Muted ? "muted" : string.Empty;

    /// <summary>Comma-separated speakers capped at <paramref name="max"/> names with a "+N" tail.</summary>
    public string SpeakingText(int max)
    {
        if (Speaking.Count == 0) return string.Empty;
        if (max <= 0 || Speaking.Count <= max) return string.Join(", ", Speaking);
        return string.Join(", ", Speaking.Take(max)) + $" +{Speaking.Count - max}";
    }
}

public sealed class VoiceStateTracker
{
    private sealed class Member
    {
        public string Name = string.Empty;
        public bool Speaking;
        /// <summary>When the last SPEAKING_STOP arrived; the member is still shown until the hold expires.</summary>
        public DateTime StoppedAt = DateTime.MinValue;
        /// <summary>Order speakers are listed in: whoever started talking first stays first.</summary>
        public DateTime StartedAt = DateTime.MinValue;
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, Member> _members = new(StringComparer.Ordinal);
    private string _channelName = string.Empty;
    private string _channelId = string.Empty;
    private bool _muted;
    private bool _deafened;

    /// <summary>How long a speaker stays listed after SPEAKING_STOP, so brief pauses do not flicker.</summary>
    public TimeSpan SpeakingHold { get; set; } = TimeSpan.FromMilliseconds(300);

    public string ChannelId { get { lock (_gate) return _channelId; } }

    public void SetChannel(string channelId, string channelName)
    {
        lock (_gate)
        {
            if (!string.Equals(_channelId, channelId, StringComparison.Ordinal)) _members.Clear();
            _channelId = channelId;
            _channelName = channelName;
        }
    }

    public void ClearChannel()
    {
        lock (_gate)
        {
            _channelId = string.Empty;
            _channelName = string.Empty;
            _members.Clear();
        }
    }

    public void SetSelf(bool? muted, bool? deafened)
    {
        lock (_gate)
        {
            if (muted.HasValue) _muted = muted.Value;
            if (deafened.HasValue) _deafened = deafened.Value;
        }
    }

    /// <summary>Adds or renames a member; speaking state is preserved across updates.</summary>
    public void UpsertMember(string userId, string displayName)
    {
        lock (_gate)
        {
            if (!_members.TryGetValue(userId, out var member)) _members[userId] = member = new Member();
            member.Name = displayName;
        }
    }

    public void RemoveMember(string userId)
    {
        lock (_gate) _members.Remove(userId);
    }

    /// <summary>Replaces the whole member list (channel join / full refresh) without losing speaking flags.</summary>
    public void ReplaceMembers(IEnumerable<(string UserId, string DisplayName)> members)
    {
        lock (_gate)
        {
            var keep = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (userId, name) in members)
            {
                keep.Add(userId);
                if (!_members.TryGetValue(userId, out var member)) _members[userId] = member = new Member();
                member.Name = name;
            }
            foreach (var gone in _members.Keys.Where(k => !keep.Contains(k)).ToList()) _members.Remove(gone);
        }
    }

    public void SetSpeaking(string userId, bool speaking, DateTime now)
    {
        lock (_gate)
        {
            if (!_members.TryGetValue(userId, out var member))
            {
                // SPEAKING_START can beat VOICE_STATE_CREATE; keep the id until a name arrives.
                _members[userId] = member = new Member { Name = userId };
            }
            if (speaking)
            {
                if (!member.Speaking) member.StartedAt = now;
                member.Speaking = true;
            }
            else
            {
                member.Speaking = false;
                member.StoppedAt = now;
            }
        }
    }

    public VoiceSnapshot Snapshot(DateTime now)
    {
        lock (_gate)
        {
            var speaking = _members.Values
                .Where(m => m.Speaking || now - m.StoppedAt < SpeakingHold)
                .OrderBy(m => m.StartedAt)
                .Select(m => m.Name)
                .ToList();
            return new VoiceSnapshot(_channelName, _channelId, _members.Count, speaking, _muted, _deafened);
        }
    }
}
