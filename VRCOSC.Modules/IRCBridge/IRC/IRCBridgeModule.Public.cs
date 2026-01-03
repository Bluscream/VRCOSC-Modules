// This is free and unencumbered software released into the public domain.
// For more information, please refer to <https://unlicense.org>

using System;
using System.Threading.Tasks;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;
using Module = VRCOSC.App.SDK.Modules.Module;

namespace Bluscream.Modules;

public partial class IRCBridgeModule
{
    public bool IsConnected => _ircClient?.IsConnected ?? false;
    public string GetChannelName() => GetVariableValue<string>(IRCBridgeVariable.ChannelName) ?? string.Empty;
    public string GetNickname() => GetVariableValue<string>(IRCBridgeVariable.Nickname) ?? string.Empty;
    public int GetUserCount() => GetVariableValue<int>(IRCBridgeVariable.UserCount);
    
    // Public accessor for GetVariableValue to be used by nodes
    public T? GetVariableValue<T>(IRCBridgeVariable variable) where T : notnull
    {
        // Use reflection to call the protected GetVariableValue<T>(Enum) method
        var method = typeof(Module).GetMethod("GetVariableValue", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null, 
            new[] { typeof(Enum) }, 
            null);
        if (method != null)
        {
            var genericMethod = method.MakeGenericMethod(typeof(T));
            return (T?)genericMethod.Invoke(this, new object[] { variable });
        }
        return default;
    }

    // Public wrapper methods
    public void SetVariableValuePublic<T>(IRCBridgeVariable variable, T value) where T : notnull
    {
        SetVariableValue(variable, value);
    }

    public void ChangeStatePublic(IRCBridgeState state)
    {
        ChangeState(state);
    }

    public void TriggerEventPublic(IRCBridgeEvent evt)
    {
        TriggerEvent(evt);
    }

    public async Task TriggerModuleNodeAsync(Type nodeType, object[] data)
    {
        try
        {
            await TriggerModuleNode(nodeType, data);
        }
        catch
        {
            // Ignore errors if node doesn't exist or module is stopping
        }
    }

    public void SendParameterPublic(IRCBridgeParameter parameter, object value)
    {
        SendParameter(parameter, value);
    }

    public void SendParameterSafePublic(IRCBridgeParameter parameter, object value)
    {
        // Don't send parameters if module is stopping or stopped
        if (!_isStopping)
        {
            try
            {
                this.SendParameterSafe(parameter, value);
            }
            catch (InvalidOperationException)
            {
                // OSC not connected, ignore during shutdown
            }
        }
    }

    public T PublicGetSettingValue<T>(IRCBridgeSetting setting) => GetSettingValue<T>(setting);
}
