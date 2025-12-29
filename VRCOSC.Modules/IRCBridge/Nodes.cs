// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

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
    public ValueInput<string> Target = new("Target (Channel or User)");

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
            var target = Target.Read(c);

            if (string.IsNullOrEmpty(message))
            {
                Error.Write("Message is required", c);
                await OnError.Execute(c);
                return;
            }

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

            // Send PRIVMSG
            await Module.SendIRCMessage($"PRIVMSG {target} :{message}");
            
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

[Node("IRC Send Channel Message")]
public sealed class IRCSendChannelMessageNode : ModuleNode<IRCBridgeModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");

    public ValueInput<string> Message = new("Message");
    public ValueInput<string> Channel = new("Channel (Optional - uses current if empty)");

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

            var channel = Channel.Read(c);
            if (string.IsNullOrEmpty(channel))
            {
                channel = Module.GetChannelName();
                if (string.IsNullOrEmpty(channel))
                {
                    Error.Write("Channel is required", c);
                    await OnError.Execute(c);
                    return;
                }
            }

            // Ensure channel starts with #
            if (!channel.StartsWith("#"))
            {
                channel = "#" + channel;
            }

            await Module.SendIRCMessage($"PRIVMSG {channel} :{message}");
            
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

[Node("IRC Send Private Message")]
public sealed class IRCSendPrivateMessageNode : ModuleNode<IRCBridgeModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");

    public ValueInput<string> Message = new("Message");
    public ValueInput<string> User = new("User (Nickname)");

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

            var user = User.Read(c);
            if (string.IsNullOrEmpty(user))
            {
                Error.Write("User is required", c);
                await OnError.Execute(c);
                return;
            }

            // Remove # if user accidentally included it
            user = user.TrimStart('#');

            await Module.SendIRCMessage($"PRIVMSG {user} :{message}");
            
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

[Node("IRC Send Notice")]
public sealed class IRCSendNoticeNode : ModuleNode<IRCBridgeModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");

    public ValueInput<string> Message = new("Notice Message");
    public ValueInput<string> Target = new("Target (Channel or User)");

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

            // Ensure channel starts with # if it's a channel
            if (!target.StartsWith("#") && !target.Contains("!"))
            {
                // Assume it's a channel if it looks like one, otherwise it's a user
                // For now, treat as user if no # prefix
            }

            await Module.SendIRCMessage($"NOTICE {target} :{message}");
            
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

            await Module.SendIRCMessage($"JOIN {channel}");
            
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
            var partMessage = string.IsNullOrEmpty(reason) 
                ? $"PART {channel}" 
                : $"PART {channel} :{reason}";

            await Module.SendIRCMessage(partMessage);
            
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
public sealed class IRCConnectionStatusNode : ModuleNode<IRCBridgeModule>
{
    public ValueOutput<bool> Connected = new("Is Connected");
    public ValueOutput<string> Channel = new("Channel Name");
    public ValueOutput<string> Nickname = new("Nickname");
    public ValueOutput<int> UserCount = new("User Count");

    public FlowCall Call = new();

    protected override Task Process(PulseContext c)
    {
        Connected.Write(Module.IsConnected, c);
        Channel.Write(Module.GetChannelName(), c);
        Nickname.Write(Module.GetNickname(), c);
        UserCount.Write(Module.GetUserCount(), c);
        return Task.CompletedTask;
    }
}

[Node("IRC Get Last Message")]
public sealed class IRCGetLastMessageNode : ModuleNode<IRCBridgeModule>
{
    public ValueOutput<string> Message = new("Last Message");
    public ValueOutput<string> User = new("Last Message User");
    public ValueOutput<string> EventTime = new("Last Event Time");

    public FlowCall Call = new();

    protected override Task Process(PulseContext c)
    {
        Message.Write(Module.GetVariableValue<string>(IRCBridgeVariable.LastMessage) ?? string.Empty, c);
        User.Write(Module.GetVariableValue<string>(IRCBridgeVariable.LastMessageUser) ?? string.Empty, c);
        EventTime.Write(Module.GetVariableValue<string>(IRCBridgeVariable.LastEventTime) ?? string.Empty, c);
        return Task.CompletedTask;
    }
}

[Node("IRC Get Last Joined User")]
public sealed class IRCGetLastJoinedUserNode : ModuleNode<IRCBridgeModule>
{
    public ValueOutput<string> User = new("Last Joined User");
    public ValueOutput<string> EventTime = new("Last Event Time");

    public FlowCall Call = new();

    protected override Task Process(PulseContext c)
    {
        User.Write(Module.GetVariableValue<string>(IRCBridgeVariable.LastJoinedUser) ?? string.Empty, c);
        EventTime.Write(Module.GetVariableValue<string>(IRCBridgeVariable.LastEventTime) ?? string.Empty, c);
        return Task.CompletedTask;
    }
}

[Node("IRC Get Last Left User")]
public sealed class IRCGetLastLeftUserNode : ModuleNode<IRCBridgeModule>
{
    public ValueOutput<string> User = new("Last Left User");
    public ValueOutput<string> EventTime = new("Last Event Time");

    public FlowCall Call = new();

    protected override Task Process(PulseContext c)
    {
        User.Write(Module.GetVariableValue<string>(IRCBridgeVariable.LastLeftUser) ?? string.Empty, c);
        EventTime.Write(Module.GetVariableValue<string>(IRCBridgeVariable.LastEventTime) ?? string.Empty, c);
        return Task.CompletedTask;
    }
}

// ============================
// IRC Event Nodes (Pulse Event Triggers)
// ============================
// These nodes are designed to be used with module events
// They expose the event data when the module triggers events

[Node("On IRC User Joined")]
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
        // Fallback to variables if Write wasn't called
        User.Write(Module.GetVariableValue<string>(IRCBridgeVariable.LastJoinedUser) ?? string.Empty, c);
        Channel.Write(Module.GetChannelName(), c);
        EventTime.Write(Module.GetVariableValue<string>(IRCBridgeVariable.LastEventTime) ?? string.Empty, c);
        
        await OnUserJoined.Execute(c);
    }
}

[Node("On IRC User Left")]
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
        // Fallback to variables if Write wasn't called
        User.Write(Module.GetVariableValue<string>(IRCBridgeVariable.LastLeftUser) ?? string.Empty, c);
        Channel.Write(Module.GetChannelName(), c);
        EventTime.Write(Module.GetVariableValue<string>(IRCBridgeVariable.LastEventTime) ?? string.Empty, c);
        
        await OnUserLeft.Execute(c);
    }
}

[Node("On IRC Message Received")]
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
        // Fallback to variables if Write wasn't called (shouldn't happen, but safe)
        Message.Write(Module.GetVariableValue<string>(IRCBridgeVariable.LastMessage) ?? string.Empty, c);
        User.Write(Module.GetVariableValue<string>(IRCBridgeVariable.LastMessageUser) ?? string.Empty, c);
        Channel.Write(Module.GetChannelName(), c);
        EventTime.Write(Module.GetVariableValue<string>(IRCBridgeVariable.LastEventTime) ?? string.Empty, c);
        
        await OnMessageReceived.Execute(c);
    }
}

[Node("On IRC Connected")]
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
        // Fallback to variables if Write wasn't called
        ServerStatus.Write(Module.GetVariableValue<string>(IRCBridgeVariable.ServerStatus) ?? string.Empty, c);
        Nickname.Write(Module.GetNickname(), c);
        
        await OnConnected.Execute(c);
    }
}

[Node("On IRC Disconnected")]
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
        // Fallback to variables if Write wasn't called
        ServerStatus.Write(Module.GetVariableValue<string>(IRCBridgeVariable.ServerStatus) ?? string.Empty, c);
        
        await OnDisconnected.Execute(c);
    }
}

[Node("On IRC Channel Joined")]
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
        // Fallback to variables if Write wasn't called
        Channel.Write(Module.GetChannelName(), c);
        UserCount.Write(Module.GetUserCount(), c);
        
        await OnChannelJoined.Execute(c);
    }
}

[Node("On IRC Error")]
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
        // Fallback to variables if Write wasn't called
        var status = Module.GetVariableValue<string>(IRCBridgeVariable.ServerStatus) ?? string.Empty;
        ErrorMessage.Write(status, c);
        ServerStatus.Write(status, c);
        
        await OnError.Execute(c);
    }
}
