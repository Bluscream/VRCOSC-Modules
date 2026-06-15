// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;

namespace Bluscream.Modules;

[ModuleTitle("Linux Process Manager")]
[ModuleDescription("Allows for starting and stopping Linux host processes from avatar parameters")]
[ModuleType(ModuleType.Integrations)]
public class LinuxProcessManagerModule : Module
{
    protected override void OnPreLoad()
    {
        RegisterParameter<bool>(ProcessManagerParameter.Start, "VRCOSC/ProcessManager/Start/*", ParameterMode.Read, "Start", "Becoming true will start the process named in the '*' that you set on your avatar\nFor example, on your avatar you put: VRCOSC/ProcessManager/Start/obs-studio");
        RegisterParameter<bool>(ProcessManagerParameter.Stop, "VRCOSC/ProcessManager/Stop/*", ParameterMode.Read, "Stop", "Becoming true will stop the process named in the '*' that you set on your avatar\nFor example, on your avatar you put: VRCOSC/ProcessManager/Stop/obs-studio");
    }

    protected override void OnRegisteredParameterReceived(RegisteredParameter parameter)
    {
        var processName = parameter.GetWildcard<string>(0);

        switch (parameter.Lookup)
        {
            case ProcessManagerParameter.Start when parameter.GetValue<bool>():
                StartProcess(processName);
                break;

            case ProcessManagerParameter.Stop when parameter.GetValue<bool>():
                StopProcess(processName);
                break;
        }
    }

    private void StartProcess(string? processName)
    {
        if (string.IsNullOrEmpty(processName)) return;

        Log($"Attempting to start host process: {processName}");
        RunBashCommand($"flatpak-spawn --host {processName} >/dev/null 2>&1 &");
    }

    private void StopProcess(string? processName)
    {
        if (string.IsNullOrEmpty(processName)) return;

        Log($"Attempting to stop host process: {processName}");
        RunBashCommand($"flatpak-spawn --host killall {processName} || flatpak-spawn --host pkill -f {processName}");
    }

    private void RunBashCommand(string command)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "Z:\\bin\\bash",
                Arguments = $"-c \"{command.Replace("\"", "\\\"")}\"",
                UseShellExecute = true,
                CreateNoWindow = true,
                WorkingDirectory = "C:\\"
            });
        }
        catch (Exception ex)
        {
            Log($"Error executing host process command: {ex.Message}");
        }
    }

    private enum ProcessManagerParameter
    {
        Start,
        Stop
    }
}
