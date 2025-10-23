// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using VRCOSC.App.Nodes;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Nodes;

namespace Bluscream.Modules;

// ============================
// VRCX Command Nodes
// ============================

[Node("VRCX Get Online Friends")]
public sealed class VRCXGetOnlineFriendsNode : ModuleNode<VRCXBridgeModule>
{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");
    
    public ValueOutput<int> Count = new("Friend Count");
    public ValueOutput<string> FriendsJson = new("Friends JSON");
    public ValueOutput<string> Error = new();

    protected override async Task Process(PulseContext c)
    {
        try
        {
            var result = await Module.GetOnlineFriends();
            
            if (result != null)
            {
                var count = result["count"]?.GetValue<int>() ?? 0;
                var data = result["data"]?.ToJsonString() ?? "[]";
                
                Count.Write(count, c);
                FriendsJson.Write(data, c);
                
                await Next.Execute(c);
            }
            else
            {
                Error.Write("No response from VRCX", c);
                await OnError.Execute(c);
            }
        }
        catch (Exception ex)
        {
            Error.Write(ex.Message, c);
            await OnError.Execute(c);
        }
    }
}

[Node("VRCX Send Invite")]
public sealed class VRCXSendInviteNode : ModuleNode<VRCXBridgeModule>{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");
    
    public ValueInput<string> UserId = new("User ID");
    public ValueInput<string> InstanceId = new("Instance ID");
    public ValueInput<string> WorldId = new("World ID");
    public ValueInput<string> WorldName = new("World Name");
    public ValueInput<string> Message = new("Message (Optional)");
    
    public ValueOutput<string> Error = new();

    protected override async Task Process(PulseContext c)
    {
        try
        {
            var userId = UserId.Read(c);
            var instanceId = InstanceId.Read(c);
            var worldId = WorldId.Read(c);
            var worldName = WorldName.Read(c);
            var message = Message.Read(c);
            
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(instanceId))
            {
                Error.Write("User ID and Instance ID are required", c);
                await OnError.Execute(c);
                return;
            }

            var result = await Module.SendInvite(userId, instanceId, worldId, worldName, 
                string.IsNullOrEmpty(message) ? null : message);
            
            if (result != null && result["success"]?.GetValue<bool>() == true)
            {
                await Next.Execute(c);
            }
            else
            {
                Error.Write(result?["error"]?.ToString() ?? "Unknown error", c);
                await OnError.Execute(c);
            }
        }
        catch (Exception ex)
        {
            Error.Write(ex.Message, c);
            await OnError.Execute(c);
        }
    }
}

[Node("VRCX Get User Info")]
public sealed class VRCXGetUserInfoNode : ModuleNode<VRCXBridgeModule>{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");
    
    public ValueInput<string> UserId = new("User ID");
    
    public ValueOutput<string> UserJson = new("User JSON");
    public new ValueOutput<string> DisplayName = new("Display Name");
    public ValueOutput<string> Error = new();

    protected override async Task Process(PulseContext c)
    {
        try
        {
            var userId = UserId.Read(c);
            
            if (string.IsNullOrEmpty(userId))
            {
                Error.Write("User ID is required", c);
                await OnError.Execute(c);
                return;
            }

            var result = await Module.GetUserInfo(userId);
            
            if (result != null && result["success"]?.GetValue<bool>() == true)
            {
                var userData = result["data"];
                UserJson.Write(userData?.ToJsonString() ?? "{}", c);
                DisplayName.Write(userData?["displayName"]?.ToString() ?? "", c);
                
                await Next.Execute(c);
            }
            else
            {
                Error.Write(result?["error"]?.ToString() ?? "Unknown error", c);
                await OnError.Execute(c);
            }
        }
        catch (Exception ex)
        {
            Error.Write(ex.Message, c);
            await OnError.Execute(c);
        }
    }
}

[Node("VRCX Get Current Location")]
public sealed class VRCXGetCurrentLocationNode : ModuleNode<VRCXBridgeModule>{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");
    
    public ValueOutput<string> WorldId = new("World ID");
    public ValueOutput<string> InstanceId = new("Instance ID");
    public ValueOutput<string> UserId = new("User ID");
    public new ValueOutput<string> DisplayName = new("Display Name");
    public ValueOutput<string> LocationJson = new("Location JSON");
    public ValueOutput<string> Error = new();

    protected override async Task Process(PulseContext c)
    {
        try
        {
            var result = await Module.GetCurrentLocation();
            
            if (result != null && result["success"]?.GetValue<bool>() == true)
            {
                var data = result["data"];
                
                WorldId.Write(data?["worldId"]?.ToString() ?? "", c);
                InstanceId.Write(data?["instanceId"]?.ToString() ?? "", c);
                UserId.Write(data?["userId"]?.ToString() ?? "", c);
                DisplayName.Write(data?["displayName"]?.ToString() ?? "", c);
                LocationJson.Write(data?.ToJsonString() ?? "{}", c);
                
                await Next.Execute(c);
            }
            else
            {
                Error.Write(result?["error"]?.ToString() ?? "Unknown error", c);
                await OnError.Execute(c);
            }
        }
        catch (Exception ex)
        {
            Error.Write(ex.Message, c);
            await OnError.Execute(c);
        }
    }
}

[Node("VRCX Show Toast")]
public sealed class VRCXShowToastNode : ModuleNode<VRCXBridgeModule>{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");
    
    public ValueInput<string> Message = new();
    public ValueInput<string> Type = new("Type (info/success/warning/error)");
    
    public ValueOutput<string> Error = new();

    protected override async Task Process(PulseContext c)
    {
        try
        {
            var message = Message.Read(c);
            var type = Type.Read(c);
            
            if (string.IsNullOrEmpty(message))
            {
                Error.Write("Message is required", c);
                await OnError.Execute(c);
                return;
            }

            var result = await Module.ShowVRCXToast(message, string.IsNullOrEmpty(type) ? "info" : type);
            
            if (result != null && result["success"]?.GetValue<bool>() == true)
            {
                await Next.Execute(c);
            }
            else
            {
                Error.Write(result?["error"]?.ToString() ?? "Unknown error", c);
                await OnError.Execute(c);
            }
        }
        catch (Exception ex)
        {
            Error.Write(ex.Message, c);
            await OnError.Execute(c);
        }
    }
}

// ============================
// Connection Status Node
// ============================

[Node("VRCX Connection Status")]
public sealed class VRCXConnectionStatusNode : ModuleNode<VRCXBridgeModule>{
    public ValueOutput<bool> Connected = new();

    public FlowCall Call = new();

    protected override Task Process(PulseContext c)
    {
        // Just output true if module is running
        Connected.Write(true, c); // Todo: Make this actually check if the connection is active
        return Task.CompletedTask;
    }
}
