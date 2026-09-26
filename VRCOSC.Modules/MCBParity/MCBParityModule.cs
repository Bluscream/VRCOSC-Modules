// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// MagicChatbox parity: every MagicChatbox placeholder whose natural home would need a pull
// request against another module or against the SDK, reimplemented here so the ChatBox
// variables exist now instead of waiting on upstream. Split by concern into partial files:
//   MCBParityModule.VR.cs        reprojection / dropped-frame estimates from the shared OpenXR runtime
//   MCBParityModule.Timezone.cs  local timezone abbreviation ("CEST") and offset ("+02:00")
// Instance master and world capacity live in VRCExtras; weather lives in the Weather module.
// Variable enum members are spelled exactly like the MagicChatbox placeholder keys because
// the SDK derives the ChatBox lookup from the enum member name (lower-cased).

using VRCOSC.App.SDK.Modules;

namespace Bluscream.Modules.MCBParity;

[ModuleTitle("MagicChatbox Parity")]
[ModuleDescription("VR reprojection/dropped-frame estimates and the local timezone abbreviation/offset as ChatBox variables (MagicChatbox placeholder parity)")]
[ModuleType(ModuleType.Generic)]
public sealed partial class MCBParityModule : Module
{
    protected override void OnPreLoad()
    {
        CreateVrSettings();
        CreateTimezoneSettings();
    }

    protected override void OnPostLoad()
    {
        var (reprojection, dropped) = CreateVrVariables();
        CreateTimezoneVariables();

        CreateState(MCBParityState.Default, "Default", "Reproj {0}% · Dropped {1}/min", new[] { reprojection, dropped });
    }

    protected override Task<bool> OnModuleStart()
    {
        StartVr();
        StartTimezone();
        ChangeState(MCBParityState.Default);
        return Task.FromResult(true);
    }

    protected override Task OnModuleStop()
    {
        StopVr();
        return Task.CompletedTask;
    }

    private enum MCBParitySetting { VrFrameStats, TimezoneOverride }

    // Spelled as the MagicChatbox placeholder keys on purpose (the lookup is the lower-cased name).
    private enum MCBParityVariable { vr_reprojection, vr_dropped_frames, timezone, timezone_offset }

    private enum MCBParityState { Default }
}
