// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;

namespace Bluscream.Modules;

[ModuleTitle("VRChat Settings")]
[ModuleDescription("Read and write VRChat registry settings and config file values")]
[ModuleType(ModuleType.Integrations)]
[ModuleInfo("https://github.com/Bluscream/VRCOSC-Modules")]
public class VRChatSettingsModule : Module
{
    private VRChatSettings? _settings;
    public VRChatSettings Settings => _settings ??= new VRChatSettings(this);

    protected override void OnPreLoad()
    {
        CreateToggle(VRChatSettingsSetting.AllowUnknownSettings, "Allow Unknown Settings", "Allow reading/writing settings not in the known list", false);
        CreateToggle(VRChatSettingsSetting.AllowOutsideLimits, "Allow Outside Known Limits", "Allow setting values outside known safe limits", false);
        CreateToggle(VRChatSettingsSetting.AllowRemoteDefinitions, "Allow Remote Definitions", "Try to load definitions from GitHub Gist (fallback to embedded)", false);
        CreateToggle(VRChatSettingsSetting.LogOperations, "Log Operations", "Log all get/set operations to console", false);
        CreateToggle(VRChatSettingsSetting.AutoBackup, "Auto Backup", "Automatically backup settings before writing", true);
        CreateTextBox(VRChatSettingsSetting.BackupDirectory, "Backup Directory", "Directory to store backups (leave empty for default)", string.Empty);

        RegisterParameter<bool>(VRChatSettingsParameter.OperationSuccess, "VRCOSC/VRChatSettings/Success", ParameterMode.Write, "Success", "True for 1 second when operation succeeds");
        RegisterParameter<bool>(VRChatSettingsParameter.OperationFailed, "VRCOSC/VRChatSettings/Failed", ParameterMode.Write, "Failed", "True for 1 second when operation fails");
        RegisterParameter<int>(VRChatSettingsParameter.OperationsCount, "VRCOSC/VRChatSettings/OperationsCount", ParameterMode.Write, "Operations Count", "Total number of successful operations");

        CreateGroup("Safety", "Safety settings", VRChatSettingsSetting.AllowUnknownSettings, VRChatSettingsSetting.AllowOutsideLimits);
        CreateGroup("Definitions", "Definition loading", VRChatSettingsSetting.AllowRemoteDefinitions);
        CreateGroup("Backup", "Backup settings", VRChatSettingsSetting.AutoBackup, VRChatSettingsSetting.BackupDirectory);
        CreateGroup("Debug", "Debug settings", VRChatSettingsSetting.LogOperations);
    }

    protected override void OnPostLoad()
    {
        var lastKeyReference = CreateVariable<string>(VRChatSettingsVariable.LastKey, "Last Key")!;
        var lastValueReference = CreateVariable<string>(VRChatSettingsVariable.LastValue, "Last Value")!;
        CreateVariable<int>(VRChatSettingsVariable.SettingsLoaded, "Settings Loaded");
        CreateVariable<int>(VRChatSettingsVariable.OperationsCount, "Operations Count");

        CreateState(VRChatSettingsState.Idle, "Idle", "VRChat Settings\nReady");
        CreateState(VRChatSettingsState.Reading, "Reading", "Reading: {0}", new[] { lastKeyReference });
        CreateState(VRChatSettingsState.Writing, "Writing", "Writing: {0}\n= {1}", new[] { lastKeyReference, lastValueReference });

        CreateEvent(VRChatSettingsEvent.OnSettingRead, "On Setting Read", "Read: {0} = {1}", new[] { lastKeyReference, lastValueReference });
        CreateEvent(VRChatSettingsEvent.OnSettingWrite, "On Setting Write", "Wrote: {0} = {1}", new[] { lastKeyReference, lastValueReference });
        CreateEvent(VRChatSettingsEvent.OnError, "On Error", "Error: {0}", new[] { lastValueReference });
    }

    protected override async Task<bool> OnModuleStart()
    {
        try
        {
            Log("VRChat Settings Module starting...");
            
            ChangeState(VRChatSettingsState.Idle);
            
            // Initialize and load definitions
            var initialized = await Settings.InitializeAsync();
            
            if (initialized)
            {
                var totalSettings = Settings.GetTotalSettingsCount();
                SetVariableValue(VRChatSettingsVariable.SettingsLoaded, totalSettings);
                Log($"VRChat Settings Module started - {totalSettings} settings loaded");
            }
            else
            {
                Log("Warning: Some definitions failed to load. Module will continue with limited functionality.");
            }
            
            SetVariableValue(VRChatSettingsVariable.OperationsCount, 0);
            
            return true;
        }
        catch (Exception ex)
        {
            Log($"Fatal error during module start: {ex.Message}");
            Log($"Stack trace: {ex.StackTrace}");
            return false;
        }
    }

    protected override Task OnModuleStop()
    {
        return Task.CompletedTask;
    }

    public bool AllowUnknownSettings => GetSettingValue<bool>(VRChatSettingsSetting.AllowUnknownSettings);
    public bool AllowOutsideLimits => GetSettingValue<bool>(VRChatSettingsSetting.AllowOutsideLimits);
    public bool AllowRemoteDefinitions => GetSettingValue<bool>(VRChatSettingsSetting.AllowRemoteDefinitions);
    public bool LogOperations => GetSettingValue<bool>(VRChatSettingsSetting.LogOperations);
    public bool AutoBackup => GetSettingValue<bool>(VRChatSettingsSetting.AutoBackup);
    public string BackupDirectory => GetSettingValue<string>(VRChatSettingsSetting.BackupDirectory) ?? string.Empty;

    public async Task SendSuccessParameter()
    {
        var wasAcknowledged = await SendParameterAndWait(VRChatSettingsParameter.OperationSuccess, true);
        if (wasAcknowledged)
            SendParameter(VRChatSettingsParameter.OperationSuccess, false);
    }

    public async Task SendFailedParameter(string error)
    {
        Log($"Operation failed: {error}");
        SetVariableValue(VRChatSettingsVariable.LastValue, error);
        TriggerEvent(VRChatSettingsEvent.OnError);
        var wasAcknowledged = await SendParameterAndWait(VRChatSettingsParameter.OperationFailed, true);
        if (wasAcknowledged)
            SendParameter(VRChatSettingsParameter.OperationFailed, false);
    }

    // Public wrapper methods for VRChatSettings class to call
    public void UpdateVariables(string key, string value, bool isWrite)
    {
        SetVariableValue(VRChatSettingsVariable.LastKey, key);
        SetVariableValue(VRChatSettingsVariable.LastValue, value);
        
        if (isWrite)
        {
            _operationsCount++;
            SetVariableValue(VRChatSettingsVariable.OperationsCount, _operationsCount);
            SendParameter(VRChatSettingsParameter.OperationsCount, _operationsCount);
            ChangeState(VRChatSettingsState.Writing);
            TriggerEvent(VRChatSettingsEvent.OnSettingWrite);
            
            // Return to idle after brief delay
            _ = Task.Delay(100).ContinueWith(_ => ChangeState(VRChatSettingsState.Idle));
        }
        else
        {
            ChangeState(VRChatSettingsState.Reading);
            TriggerEvent(VRChatSettingsEvent.OnSettingRead);
            
            // Return to idle after brief delay
            _ = Task.Delay(100).ContinueWith(_ => ChangeState(VRChatSettingsState.Idle));
        }
    }

    private int _operationsCount = 0;

    public enum VRChatSettingsSetting
    {
        AllowUnknownSettings,
        AllowOutsideLimits,
        AllowRemoteDefinitions,
        LogOperations,
        AutoBackup,
        BackupDirectory
    }

    public enum VRChatSettingsParameter
    {
        OperationSuccess,
        OperationFailed,
        OperationsCount
    }

    public enum VRChatSettingsState
    {
        Idle,
        Reading,
        Writing
    }

    public enum VRChatSettingsVariable
    {
        LastKey,
        LastValue,
        SettingsLoaded,
        OperationsCount
    }

    public enum VRChatSettingsEvent
    {
        OnSettingRead,
        OnSettingWrite,
        OnError
    }
}
