// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using VRCOSC.App.Nodes;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Nodes;

namespace Bluscream.Modules;

// Registry Get Node
[Node("Get VRChat Registry Setting")]
public sealed class GetRegistrySettingNode : ModuleNode<VRChatSettingsModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");
    
    public ValueInput<string> Key = new("Setting Key");
    public ValueInput<string> UserId = new("User ID (Optional)");
    
    public ValueOutput<string> StringValue = new("String Value");
    public ValueOutput<int> IntValue = new("Int Value");
    public ValueOutput<float> FloatValue = new("Float Value");
    public ValueOutput<bool> BoolValue = new("Bool Value");
    public ValueOutput<string> Error = new();

    protected override async Task Process(PulseContext c)
    {
        try
        {
            var key = Key.Read(c);
            var userId = UserId.Read(c);
            
            if (string.IsNullOrEmpty(key))
            {
                Error.Write("Key is required", c);
                await OnError.Execute(c);
                return;
            }

            // Try to get as object first, then convert to specific types
            if (Module.Settings.GetRegistrySetting<object>(key, out var value, out var error, userId))
            {
                if (value != null)
                {
                    StringValue.Write(value.ToString() ?? string.Empty, c);
                    
                    // Try to convert to other types
                    try
                    {
                        if (value is int intVal)
                            IntValue.Write(intVal, c);
                        else if (int.TryParse(value.ToString(), out var parsedInt))
                            IntValue.Write(parsedInt, c);
                    }
                    catch { }

                    try
                    {
                        if (value is float floatVal)
                            FloatValue.Write(floatVal, c);
                        else if (value is double doubleVal)
                            FloatValue.Write((float)doubleVal, c);
                        else if (float.TryParse(value.ToString(), out var parsedFloat))
                            FloatValue.Write(parsedFloat, c);
                    }
                    catch { }

                    try
                    {
                        if (value is bool boolVal)
                            BoolValue.Write(boolVal, c);
                        else if (bool.TryParse(value.ToString(), out var parsedBool))
                            BoolValue.Write(parsedBool, c);
                    }
                    catch { }
                }

                await Module.SendSuccessParameter();
                await Next.Execute(c);
            }
            else
            {
                Error.Write(error, c);
                await Module.SendFailedParameter(error);
                await OnError.Execute(c);
            }
        }
        catch (Exception ex)
        {
            Error.Write(ex.Message, c);
            await Module.SendFailedParameter(ex.Message);
            await OnError.Execute(c);
        }
    }
}

// Registry Get Generic Node
[Node("Get VRChat Registry Value")]
public sealed class GetRegistryValueNode<T> : ModuleNode<VRChatSettingsModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");
    
    public ValueInput<string> Key = new("Setting Key");
    public ValueInput<string> UserId = new("User ID (Optional)");
    
    public ValueOutput<T> Value = new();
    public ValueOutput<string> Error = new();

    protected override async Task Process(PulseContext c)
    {
        try
        {
            var key = Key.Read(c);
            var userId = UserId.Read(c);
            
            if (string.IsNullOrEmpty(key))
            {
                Error.Write("Key is required", c);
                await OnError.Execute(c);
                return;
            }

            if (Module.Settings.GetRegistrySetting<T>(key, out var value, out var error, userId))
            {
                Value.Write(value!, c);
                await Module.SendSuccessParameter();
                await Next.Execute(c);
            }
            else
            {
                Error.Write(error, c);
                await Module.SendFailedParameter(error);
                await OnError.Execute(c);
            }
        }
        catch (Exception ex)
        {
            Error.Write(ex.Message, c);
            await Module.SendFailedParameter(ex.Message);
            await OnError.Execute(c);
        }
    }
}

// Config File Get Node
[Node("Get VRChat Config Setting")]
public sealed class GetConfigSettingNode : ModuleNode<VRChatSettingsModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");
    
    public ValueInput<string> Key = new("Setting Key");
    
    public ValueOutput<string> StringValue = new("String Value");
    public ValueOutput<int> IntValue = new("Int Value");
    public ValueOutput<float> FloatValue = new("Float Value");
    public ValueOutput<bool> BoolValue = new("Bool Value");
    public ValueOutput<string> Error = new();

    protected override async Task Process(PulseContext c)
    {
        try
        {
            var key = Key.Read(c);
            
            if (string.IsNullOrEmpty(key))
            {
                Error.Write("Key is required", c);
                await OnError.Execute(c);
                return;
            }

            if (Module.Settings.GetConfigSetting<object>(key, out var value, out var error))
            {
                if (value != null)
                {
                    StringValue.Write(value.ToString() ?? string.Empty, c);
                    
                    // Try to convert to other types
                    try
                    {
                        if (value is int intVal)
                            IntValue.Write(intVal, c);
                        else if (int.TryParse(value.ToString(), out var parsedInt))
                            IntValue.Write(parsedInt, c);
                    }
                    catch { }

                    try
                    {
                        if (value is float floatVal)
                            FloatValue.Write(floatVal, c);
                        else if (value is double doubleVal)
                            FloatValue.Write((float)doubleVal, c);
                        else if (float.TryParse(value.ToString(), out var parsedFloat))
                            FloatValue.Write(parsedFloat, c);
                    }
                    catch { }

                    try
                    {
                        if (value is bool boolVal)
                            BoolValue.Write(boolVal, c);
                        else if (bool.TryParse(value.ToString(), out var parsedBool))
                            BoolValue.Write(parsedBool, c);
                    }
                    catch { }
                }

                await Module.SendSuccessParameter();
                await Next.Execute(c);
            }
            else
            {
                Error.Write(error, c);
                await Module.SendFailedParameter(error);
                await OnError.Execute(c);
            }
        }
        catch (Exception ex)
        {
            Error.Write(ex.Message, c);
            await Module.SendFailedParameter(ex.Message);
            await OnError.Execute(c);
        }
    }
}

// Config Get Generic Node
[Node("Get VRChat Config Value")]
public sealed class GetConfigValueNode<T> : ModuleNode<VRChatSettingsModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");
    
    public ValueInput<string> Key = new("Setting Key");
    
    public ValueOutput<T> Value = new();
    public ValueOutput<string> Error = new();

    protected override async Task Process(PulseContext c)
    {
        try
        {
            var key = Key.Read(c);
            
            if (string.IsNullOrEmpty(key))
            {
                Error.Write("Key is required", c);
                await OnError.Execute(c);
                return;
            }

            if (Module.Settings.GetConfigSetting<T>(key, out var value, out var error))
            {
                Value.Write(value!, c);
                await Module.SendSuccessParameter();
                await Next.Execute(c);
            }
            else
            {
                Error.Write(error, c);
                await Module.SendFailedParameter(error);
                await OnError.Execute(c);
            }
        }
        catch (Exception ex)
        {
            Error.Write(ex.Message, c);
            await Module.SendFailedParameter(ex.Message);
            await OnError.Execute(c);
        }
    }
}

// List All Settings Nodes
[Node("Get All VRChat Registry Settings")]
public sealed class ListAllRegistrySettingsNode : ModuleNode<VRChatSettingsModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");
    
    public ValueOutput<Dictionary<string, object>> Settings = new("Settings Dictionary");
    public ValueOutput<int> Count = new("Settings Count");
    public ValueOutput<string> Error = new();

    protected override async Task Process(PulseContext c)
    {
        try
        {
            var settings = Module.Settings.ListAllRegistrySettings(out var error);
            if (string.IsNullOrEmpty(error))
            {
                Settings.Write(settings, c);
                Count.Write(settings.Count, c);
                
                await Module.SendSuccessParameter();
                await Next.Execute(c);
            }
            else
            {
                Error.Write(error, c);
                await Module.SendFailedParameter(error);
                await OnError.Execute(c);
            }
        }
        catch (Exception ex)
        {
            Error.Write(ex.Message, c);
            await Module.SendFailedParameter(ex.Message);
            await OnError.Execute(c);
        }
    }
}

[Node("Get All VRChat Config Settings")]
public sealed class ListAllConfigSettingsNode : ModuleNode<VRChatSettingsModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");
    
    public ValueOutput<Dictionary<string, object>> Settings = new("Settings Dictionary");
    public ValueOutput<int> Count = new("Settings Count");
    public ValueOutput<string> Error = new();

    protected override async Task Process(PulseContext c)
    {
        try
        {
            var settings = Module.Settings.ListAllConfigSettings(out var error);
            if (string.IsNullOrEmpty(error))
            {
                Settings.Write(settings, c);
                Count.Write(settings.Count, c);
                
                await Module.SendSuccessParameter();
                await Next.Execute(c);
            }
            else
            {
                Error.Write(error, c);
                await Module.SendFailedParameter(error);
                await OnError.Execute(c);
            }
        }
        catch (Exception ex)
        {
            Error.Write(ex.Message, c);
            await Module.SendFailedParameter(ex.Message);
            await OnError.Execute(c);
        }
    }
}

// Utility Nodes
[Node("Object To JSON String")] // TODO: Remove once officially implemented in VRCOSC
public sealed class ObjectToJsonNode<T> : ModuleNode<VRChatSettingsModule>
{
    public ValueInput<T> Object = new("Input Object");
    public ValueInput<bool> Indented = new("Indented");
    
    public ValueOutput<string> Json = new("JSON String");
    public ValueOutput<int> Length = new("Length");

    protected override Task Process(PulseContext c)
    {
        try
        {
            var obj = Object.Read(c);
            var indented = Indented.Read(c);

            if (obj == null)
            {
                Json.Write("null", c);
                Length.Write(4, c);
                return Task.CompletedTask;
            }

            var options = new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = indented 
            };

            var json = System.Text.Json.JsonSerializer.Serialize(obj, options);
            Json.Write(json, c);
            Length.Write(json.Length, c);
        }
        catch (Exception ex)
        {
            Json.Write($"Error: {ex.Message}", c);
            Length.Write(0, c);
        }

        return Task.CompletedTask;
    }
}

// Generic/Multi-type nodes (leveraging <T> pattern)
[Node("Set VRChat Registry Value")]
public sealed class SetRegistryValueNode<T> : ModuleNode<VRChatSettingsModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");
    
    public ValueInput<string> Key = new("Setting Key");
    public ValueInput<T> Value = new();
    public ValueInput<string> UserId = new("User ID (Optional)");
    
    public ValueOutput<string> Error = new();

    protected override async Task Process(PulseContext c)
    {
        try
        {
            var key = Key.Read(c);
            var value = Value.Read(c);
            var userId = UserId.Read(c);
            
            if (string.IsNullOrEmpty(key))
            {
                Error.Write("Key is required", c);
                await OnError.Execute(c);
                return;
            }

            if (Module.Settings.SetRegistrySetting(key, value, out var error, userId))
            {
                await Module.SendSuccessParameter();
                await Next.Execute(c);
            }
            else
            {
                Error.Write(error, c);
                await Module.SendFailedParameter(error);
                await OnError.Execute(c);
            }
        }
        catch (Exception ex)
        {
            Error.Write(ex.Message, c);
            await Module.SendFailedParameter(ex.Message);
            await OnError.Execute(c);
        }
    }
}

[Node("Set VRChat Config Value")]
public sealed class SetConfigValueNode<T> : ModuleNode<VRChatSettingsModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");
    
    public ValueInput<string> Key = new("Setting Key");
    public ValueInput<T> Value = new();
    
    public ValueOutput<string> Error = new();

    protected override async Task Process(PulseContext c)
    {
        try
        {
            var key = Key.Read(c);
            var value = Value.Read(c);
            
            if (string.IsNullOrEmpty(key))
            {
                Error.Write("Key is required", c);
                await OnError.Execute(c);
                return;
            }

            if (Module.Settings.SetConfigSetting(key, value, out var error))
            {
                await Module.SendSuccessParameter();
                await Next.Execute(c);
            }
            else
            {
                Error.Write(error, c);
                await Module.SendFailedParameter(error);
                await OnError.Execute(c);
            }
        }
        catch (Exception ex)
        {
            Error.Write(ex.Message, c);
            await Module.SendFailedParameter(ex.Message);
            await OnError.Execute(c);
        }
    }
}
