// This is free and unencumbered software released into the public domain.
// For more information, please refer to <https://unlicense.org>

using System.Collections.Generic;
using System.Threading.Tasks;
using VRCOSC.App.Nodes;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Nodes;

namespace Bluscream.Modules;

// ============================
// IRC Connection Nodes
// ============================

[Node("IRC Connect")]
public sealed class IRCConnectNode : ModuleNode<IRCBridgeModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");

    public ValueOutput<bool> Success = new();
    public ValueOutput<string> Error = new();

    protected override async Task Process(PulseContext c)
    {
        try
        {
            if (Module.IsConnected)
            {
                Success.Write(true, c);
                await Next.Execute(c);
                return;
            }

            await Module.ConnectToIRC();
            
            // Wait a bit for connection to establish
            await Task.Delay(2000);
            
            if (Module.IsConnected)
            {
                Success.Write(true, c);
                await Next.Execute(c);
            }
            else
            {
                Success.Write(false, c);
                Error.Write("Failed to connect to IRC server", c);
                await OnError.Execute(c);
            }
        }
        catch (System.Exception ex)
        {
            Success.Write(false, c);
            Error.Write(ex.Message, c);
            await OnError.Execute(c);
        }
    }
}

[Node("IRC Disconnect")]
public sealed class IRCDisconnectNode : ModuleNode<IRCBridgeModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");

    public ValueOutput<bool> Success = new();

    protected override async Task Process(PulseContext c)
    {
        try
        {
            Module.DisconnectFromIRC();
            Success.Write(true, c);
            await Next.Execute(c);
        }
        catch (System.Exception)
        {
            Success.Write(false, c);
            await Next.Execute(c);
        }
    }
}

[Node("IRC Send Message")]
public sealed class IRCSendMessageNode : ModuleNode<IRCBridgeModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");

    public ValueInput<string> Message = new("Message");
    public ValueInput<string> Target = new("Target (#channel, @user, notice:target, raw:COMMAND)");

    public ValueOutput<bool> Success = new();
    public ValueOutput<string> Error = new();

    protected override async Task Process(PulseContext c)
    {
        try
        {
            if (!Module.IsConnected)
            {
                Error.Write("Not connected to IRC server", c);
                await OnError.Execute(c);
                return;
            }

            var message = Message.Read(c);
            if (string.IsNullOrEmpty(message))
            {
                Error.Write("Message is required", c);
                await OnError.Execute(c);
                return;
            }

            var target = Target.Read(c);
            if (string.IsNullOrEmpty(target))
            {
                // Use current channel if target not specified
                target = Module.GetChannelName();
                if (string.IsNullOrEmpty(target))
                {
                    Error.Write("Target is required", c);
                    await OnError.Execute(c);
                    return;
                }
            }

            // Parse target format:
            // #channel = channel message
            // @user = private message
            // notice:target = notice to target
            // raw:COMMAND params = raw IRC command

            if (target.StartsWith("raw:", StringComparison.OrdinalIgnoreCase))
            {
                // Raw IRC command: "raw:COMMAND param1 param2 :trailing param"
                var rawCommand = target.Substring(4).TrimStart(); // Remove "raw:" prefix
                await Module.SendIRCMessage($"{rawCommand} {message}");
            }
            else if (target.StartsWith("notice:", StringComparison.OrdinalIgnoreCase))
            {
                // Notice: "notice:#channel" or "notice:user"
                var noticeTarget = target.Substring(7).TrimStart(); // Remove "notice:" prefix
                await Module.SendNoticeAsync(noticeTarget, message);
            }
            else if (target.StartsWith("#"))
            {
                // Channel message
                await Module.SendMessageToChannelAsync(target, message);
            }
            else if (target.StartsWith("@"))
            {
                // Private message to user
                var user = target.Substring(1).TrimStart(); // Remove "@" prefix
                await Module.SendMessageToUserAsync(user, message);
            }
            else
            {
                // Default: treat as user (backward compatibility)
                await Module.SendMessageToUserAsync(target, message);
            }
            
            Success.Write(true, c);
            await Next.Execute(c);
        }
        catch (System.Exception ex)
        {
            Success.Write(false, c);
            Error.Write(ex.Message, c);
            await OnError.Execute(c);
        }
    }
}

[Node("IRC Join Channel")]
public sealed class IRCJoinChannelNode : ModuleNode<IRCBridgeModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");

    public ValueInput<string> Channel = new("Channel");

    public ValueOutput<bool> Success = new();
    public ValueOutput<string> Error = new();

    protected override async Task Process(PulseContext c)
    {
        try
        {
            if (!Module.IsConnected)
            {
                Error.Write("Not connected to IRC server", c);
                await OnError.Execute(c);
                return;
            }

            var channel = Channel.Read(c);
            if (string.IsNullOrEmpty(channel))
            {
                Error.Write("Channel is required", c);
                await OnError.Execute(c);
                return;
            }

            // Ensure channel starts with #
            if (!channel.StartsWith("#"))
            {
                channel = "#" + channel;
            }

            await Module.JoinChannelAsync(channel);
            
            Success.Write(true, c);
            await Next.Execute(c);
        }
        catch (System.Exception ex)
        {
            Success.Write(false, c);
            Error.Write(ex.Message, c);
            await OnError.Execute(c);
        }
    }
}

[Node("IRC Leave Channel")]
public sealed class IRCLeaveChannelNode : ModuleNode<IRCBridgeModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");

    public ValueInput<string> Channel = new("Channel");
    public ValueInput<string> Reason = new("Reason (Optional)");

    public ValueOutput<bool> Success = new();

    protected override async Task Process(PulseContext c)
    {
        try
        {
            if (!Module.IsConnected)
            {
                Success.Write(false, c);
                await Next.Execute(c);
                return;
            }

            var channel = Channel.Read(c);
            if (string.IsNullOrEmpty(channel))
            {
                // Use current channel if not specified
                channel = Module.GetChannelName();
            }

            if (string.IsNullOrEmpty(channel))
            {
                Success.Write(false, c);
                await Next.Execute(c);
                return;
            }

            // Ensure channel starts with #
            if (!channel.StartsWith("#"))
            {
                channel = "#" + channel;
            }

            var reason = Reason.Read(c);
            await Module.LeaveChannelAsync(channel, string.IsNullOrEmpty(reason) ? null : reason);
            
            Success.Write(true, c);
            await Next.Execute(c);
        }
        catch (System.Exception)
        {
            Success.Write(false, c);
            await Next.Execute(c);
        }
    }
}

// ============================
// IRC Status Nodes
// ============================

[Node("IRC Connection Status")]
public sealed class IRCConnectionStatusNode : ModuleNode<IRCBridgeModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public FlowCall Call = new();

    public ValueOutput<bool> Connected = new("Is Connected");
    public ValueOutput<string> Channel = new("Channel Name");
    public ValueOutput<string> Nickname = new("Nickname");
    public ValueOutput<int> UserCount = new("User Count");

    protected override async Task Process(PulseContext c)
    {
        Connected.Write(Module.IsConnected, c);
        Channel.Write(Module.GetChannelName(), c);
        Nickname.Write(Module.GetNickname(), c);
        UserCount.Write(Module.GetUserCount(), c);
        await Next.Execute(c);
    }
}

[Node("IRC Get Last Message")]
public sealed class IRCGetLastMessageNode : ModuleNode<IRCBridgeModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public FlowCall Call = new();

    public ValueOutput<string> Message = new("Last Message");
    public ValueOutput<string> User = new("Last Message User");
    public ValueOutput<string> EventTime = new("Last Event Time");

    protected override async Task Process(PulseContext c)
    {
        Message.Write(Module.GetVariableValue<string>(IRCBridgeVariable.LastMessage) ?? string.Empty, c);
        User.Write(Module.GetVariableValue<string>(IRCBridgeVariable.LastMessageUser) ?? string.Empty, c);
        EventTime.Write(Module.GetVariableValue<string>(IRCBridgeVariable.LastEventTime) ?? string.Empty, c);
        await Next.Execute(c);
    }
}

[Node("IRC Get Last Joined User")]
public sealed class IRCGetLastJoinedUserNode : ModuleNode<IRCBridgeModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public FlowCall Call = new();

    public ValueOutput<string> User = new("Last Joined User");
    public ValueOutput<string> EventTime = new("Last Event Time");

    protected override async Task Process(PulseContext c)
    {
        User.Write(Module.GetVariableValue<string>(IRCBridgeVariable.LastJoinedUser) ?? string.Empty, c);
        EventTime.Write(Module.GetVariableValue<string>(IRCBridgeVariable.LastEventTime) ?? string.Empty, c);
        await Next.Execute(c);
    }
}

[Node("IRC Get Last Left User")]
public sealed class IRCGetLastLeftUserNode : ModuleNode<IRCBridgeModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public FlowCall Call = new();

    public ValueOutput<string> User = new("Last Left User");
    public ValueOutput<string> EventTime = new("Last Event Time");

    protected override async Task Process(PulseContext c)
    {
        User.Write(Module.GetVariableValue<string>(IRCBridgeVariable.LastLeftUser) ?? string.Empty, c);
        EventTime.Write(Module.GetVariableValue<string>(IRCBridgeVariable.LastEventTime) ?? string.Empty, c);
        await Next.Execute(c);
    }
}

[Node("IRC Get Channel User List")]
public sealed class IRCGetChannelUserListNode : ModuleNode<IRCBridgeModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");

    public ValueInput<string> Channel = new("Channel (Optional)");

    public ValueOutput<List<string>> UserList = new("User List");
    public ValueOutput<int> UserCount = new("User Count");
    public ValueOutput<string> Error = new("Error");

    protected override async Task Process(PulseContext c)
    {
        try
        {
            if (!Module.IsConnected)
            {
                Error.Write("Not connected to IRC server", c);
                await OnError.Execute(c);
                return;
            }

            var channel = Channel.Read(c);
            var userList = Module.GetChannelUserList(string.IsNullOrEmpty(channel) ? null : channel);
            
            UserList.Write(userList, c);
            UserCount.Write(userList.Count, c);
            await Next.Execute(c);
        }
        catch (System.Exception ex)
        {
            Error.Write(ex.Message, c);
            await OnError.Execute(c);
        }
    }
}

[Node("IRC Change Nickname")]
public sealed class IRCChangeNicknameNode : ModuleNode<IRCBridgeModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");

    public ValueInput<string> NewNickname = new("New Nickname");

    public ValueOutput<bool> Success = new("Success");
    public ValueOutput<string> Error = new("Error");

    protected override async Task Process(PulseContext c)
    {
        try
        {
            if (!Module.IsConnected)
            {
                Error.Write("Not connected to IRC server", c);
                Success.Write(false, c);
                await OnError.Execute(c);
                return;
            }

            var newNickname = NewNickname.Read(c);
            if (string.IsNullOrWhiteSpace(newNickname))
            {
                Error.Write("Nickname cannot be empty", c);
                Success.Write(false, c);
                await OnError.Execute(c);
                return;
            }

            await Module.ChangeNicknameAsync(newNickname);
            
            Success.Write(true, c);
            await Next.Execute(c);
        }
        catch (System.Exception ex)
        {
            Success.Write(false, c);
            Error.Write(ex.Message, c);
            await OnError.Execute(c);
        }
    }
}

// ============================
// IRC Event Nodes (Pulse Event Triggers)
// ============================
// These nodes are designed to be used with module events
// They expose the event data when the module triggers events

[Node("On IRC User Joined")]
[NodeNoCancel]
public sealed class OnIRCUserJoinedNode : ModuleNode<IRCBridgeModule>, IModuleNodeEventHandler
{
    public FlowCall OnUserJoined = new("On User Joined");

    public ValueOutput<string> User = new("User");
    public ValueOutput<string> Channel = new("Channel");
    public ValueOutput<string> EventTime = new("Event Time");

    public Task Write(object[] args, PulseContext c)
    {
        // Args: [0] = user, [1] = channel, [2] = eventTime
        if (args.Length >= 1) User.Write(args[0] as string ?? string.Empty, c);
        if (args.Length >= 2) Channel.Write(args[1] as string ?? string.Empty, c);
        if (args.Length >= 3) EventTime.Write(args[2] as string ?? string.Empty, c);
        return Task.CompletedTask;
    }

    protected override async Task Process(PulseContext c)
    {
        await OnUserJoined.Execute(c);
    }
}

[Node("On IRC User Left")]
[NodeNoCancel]
public sealed class OnIRCUserLeftNode : ModuleNode<IRCBridgeModule>, IModuleNodeEventHandler
{
    public FlowCall OnUserLeft = new("On User Left");

    public ValueOutput<string> User = new("User");
    public ValueOutput<string> Channel = new("Channel");
    public ValueOutput<string> EventTime = new("Event Time");

    public Task Write(object[] args, PulseContext c)
    {
        // Args: [0] = user, [1] = channel, [2] = eventTime
        if (args.Length >= 1) User.Write(args[0] as string ?? string.Empty, c);
        if (args.Length >= 2) Channel.Write(args[1] as string ?? string.Empty, c);
        if (args.Length >= 3) EventTime.Write(args[2] as string ?? string.Empty, c);
        return Task.CompletedTask;
    }

    protected override async Task Process(PulseContext c)
    {
        await OnUserLeft.Execute(c);
    }
}

[Node("On IRC Message Received")]
[NodeNoCancel]
public sealed class OnIRCMessageReceivedNode : ModuleNode<IRCBridgeModule>, IModuleNodeEventHandler
{
    public FlowCall OnMessageReceived = new("On Message Received");

    public ValueOutput<string> Message = new("Message");
    public ValueOutput<string> User = new("User");
    public ValueOutput<string> Channel = new("Channel");
    public ValueOutput<string> EventTime = new("Event Time");

    public Task Write(object[] args, PulseContext c)
    {
        // Args: [0] = message, [1] = user, [2] = channel, [3] = eventTime
        if (args.Length >= 1) Message.Write(args[0] as string ?? string.Empty, c);
        if (args.Length >= 2) User.Write(args[1] as string ?? string.Empty, c);
        if (args.Length >= 3) Channel.Write(args[2] as string ?? string.Empty, c);
        if (args.Length >= 4) EventTime.Write(args[3] as string ?? string.Empty, c);
        return Task.CompletedTask;
    }

    protected override async Task Process(PulseContext c)
    {
        await OnMessageReceived.Execute(c);
    }
}

[Node("On IRC Connected")]
[NodeNoCancel]
public sealed class OnIRCConnectedNode : ModuleNode<IRCBridgeModule>, IModuleNodeEventHandler
{
    public FlowCall OnConnected = new("On Connected");

    public ValueOutput<string> ServerStatus = new("Server Status");
    public ValueOutput<string> Nickname = new("Nickname");

    public Task Write(object[] args, PulseContext c)
    {
        // Args: [0] = serverStatus, [1] = nickname
        if (args.Length >= 1) ServerStatus.Write(args[0] as string ?? string.Empty, c);
        if (args.Length >= 2) Nickname.Write(args[1] as string ?? string.Empty, c);
        return Task.CompletedTask;
    }

    protected override async Task Process(PulseContext c)
    {
        await OnConnected.Execute(c);
    }
}

[Node("On IRC Disconnected")]
[NodeNoCancel]
public sealed class OnIRCDisconnectedNode : ModuleNode<IRCBridgeModule>, IModuleNodeEventHandler
{
    public FlowCall OnDisconnected = new("On Disconnected");

    public ValueOutput<string> ServerStatus = new("Server Status");

    public Task Write(object[] args, PulseContext c)
    {
        // Args: [0] = serverStatus
        if (args.Length >= 1) ServerStatus.Write(args[0] as string ?? string.Empty, c);
        return Task.CompletedTask;
    }

    protected override async Task Process(PulseContext c)
    {
        await OnDisconnected.Execute(c);
    }
}

[Node("On IRC Channel Joined")]
[NodeNoCancel]
public sealed class OnIRCChannelJoinedNode : ModuleNode<IRCBridgeModule>, IModuleNodeEventHandler
{
    public FlowCall OnChannelJoined = new("On Channel Joined");

    public ValueOutput<string> Channel = new("Channel");
    public ValueOutput<int> UserCount = new("User Count");

    public Task Write(object[] args, PulseContext c)
    {
        // Args: [0] = channel, [1] = userCount
        if (args.Length >= 1) Channel.Write(args[0] as string ?? string.Empty, c);
        if (args.Length >= 2 && args[1] is int count) UserCount.Write(count, c);
        return Task.CompletedTask;
    }

    protected override async Task Process(PulseContext c)
    {
        await OnChannelJoined.Execute(c);
    }
}

[Node("On IRC Error")]
[NodeNoCancel]
public sealed class OnIRCErrorNode : ModuleNode<IRCBridgeModule>, IModuleNodeEventHandler
{
    public FlowCall OnError = new("On Error");

    public ValueOutput<string> ErrorMessage = new("Error Message");
    public ValueOutput<string> ServerStatus = new("Server Status");

    public Task Write(object[] args, PulseContext c)
    {
        // Args: [0] = errorMessage, [1] = serverStatus
        if (args.Length >= 1) ErrorMessage.Write(args[0] as string ?? string.Empty, c);
        if (args.Length >= 2) ServerStatus.Write(args[1] as string ?? string.Empty, c);
        return Task.CompletedTask;
    }

    protected override async Task Process(PulseContext c)
    {
        await OnError.Execute(c);
    }
}
