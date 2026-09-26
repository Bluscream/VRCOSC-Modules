// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// A status line for the ChatBox, modelled on MagicChatbox's status list: a list of texts,
// one active at a time, optionally cycled round-robin or by weighted random, with groups
// that can be switched on and off and an optional icon prefix.

using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;

namespace Bluscream.Modules.Status;

[ModuleTitle("Status")]
[ModuleDescription("A list of status texts shown one at a time in the ChatBox, with optional cycling, groups and an icon prefix")]
[ModuleType(ModuleType.Generic)]
public class StatusModule : Module
{
    private readonly Random _random = new();

    /// <summary>Text of the active status. Persisted so a restart resumes where it left off.</summary>
    [ModulePersistent("active_text")]
    public string ActiveText { get; set; } = string.Empty;

    /// <summary>When each status text was last shown; drives the weighted-random order.</summary>
    [ModulePersistent("last_used")]
    public Dictionary<string, DateTime> LastUsed { get; set; } = new();

    private bool _cycling;
    private DateTime _lastSwitch = DateTime.MinValue;
    private int _iconIndex;
    private string _currentIcon = string.Empty;
    private bool _hasActive;

    protected override void OnPreLoad()
    {
        CreateCustomSetting(StatusSetting.Statuses, new StatusListModuleSetting());
        CreateToggle(StatusSetting.Cycle, "Cycle statuses", "Take turns showing every status marked for cycling. Can also be toggled at runtime through the Cycle parameter.", false);
        CreateTextBox(StatusSetting.Interval, "Interval (seconds)", "Seconds each status stays before the next one", 30);
        CreateToggle(StatusSetting.Random, "Random order", "Pick the next status by weighted random (statuses shown longest ago are most likely) instead of top to bottom", false);
        CreateTextBoxList(StatusSetting.DisabledGroups, "Disabled groups", "Group names whose statuses are skipped while cycling", Array.Empty<string>());
        CreateTextBox(StatusSetting.OverrideGroup, "Only cycle this group", "When set, cycling only uses statuses in this group", string.Empty);
        CreateToggle(StatusSetting.PrefixIcon, "Icon in front", "Put an icon in front of the status text in the Status variable. The icon is also available on its own as the Icon variable.", true);
        CreateTextBoxList(StatusSetting.Icons, "Icons", "One is used per status switch, in order or shuffled", new[] { "\U0001F4AC" });
        CreateToggle(StatusSetting.ShuffleIcons, "Shuffle icons", "Pick a random icon on each switch instead of going in order", false);

        CreateGroup("Cycling", "How and when the active status changes", StatusSetting.Cycle, StatusSetting.Interval, StatusSetting.Random, StatusSetting.DisabledGroups, StatusSetting.OverrideGroup);
        CreateGroup("Icon", "Optional icon in front of the status", StatusSetting.PrefixIcon, StatusSetting.Icons, StatusSetting.ShuffleIcons);

        RegisterParameter<bool>(StatusParameter.Next, "VRCOSC/Status/Next", ParameterMode.Read, "Next", "Becoming true switches to the next status");
        RegisterParameter<bool>(StatusParameter.Previous, "VRCOSC/Status/Previous", ParameterMode.Read, "Previous", "Becoming true switches to the previous status");
        RegisterParameter<bool>(StatusParameter.Cycle, "VRCOSC/Status/Cycle", ParameterMode.ReadWrite, "Cycle", "Whether statuses are cycling; write to turn cycling on or off");
        RegisterParameter<int>(StatusParameter.Index, "VRCOSC/Status/Index", ParameterMode.ReadWrite, "Index", "Index of the active status in the list; write to select one");
    }

    protected override void OnPostLoad()
    {
        var status = CreateVariable<string>(StatusVariable.Status, "Status (with icon)")!;
        CreateVariable<string>(StatusVariable.Text, "Status text");
        CreateVariable<string>(StatusVariable.Icon, "Icon");
        CreateVariable<int>(StatusVariable.Index, "Index");
        CreateVariable<int>(StatusVariable.Count, "Count");
        CreateVariable<string>(StatusVariable.Group, "Group");

        CreateState(StatusState.Default, "Default", "{0}", new[] { status });
        CreateState(StatusState.Idle, "No status", string.Empty);

        CreateEvent(StatusEvent.Changed, "Status changed", "{0}", new[] { status });
    }

    protected override Task<bool> OnModuleStart()
    {
        _cycling = GetSettingValue<bool>(StatusSetting.Cycle);
        _lastSwitch = DateTime.UtcNow;
        _hasActive = false;
        _currentIcon = PickIcon(first: true);

        var rows = Rows();
        var active = rows.FirstOrDefault(r => r.Text.Value == ActiveText) ?? rows.FirstOrDefault();
        if (active is not null) ActiveText = active.Text.Value;

        // Idle until the first ApplyActive so a compound state never shows an empty {0}.
        ChangeState(StatusState.Idle);
        ApplyActive(active, rows);
        SendParameter(StatusParameter.Cycle, _cycling);
        return Task.FromResult(true);
    }

    protected override Task OnModuleStop()
    {
        ChangeState(StatusState.Idle);
        return Task.CompletedTask;
    }

    [ModuleUpdate(ModuleUpdateMode.Custom, true, 1000)]
    private void Tick()
    {
        if (!Bluscream.ModuleUtils.IsStarted()) return;

        var rows = Rows();
        var active = ActiveRow(rows);

        // The list is editable while running: keep variables in sync with the current text
        // (an edited active row keeps its position), and recover if the active row was removed.
        if (active is null && rows.Count > 0)
        {
            SwitchTo(rows[0], rows, announce: false);
            return;
        }

        if (!_cycling || rows.Count == 0) return;

        var interval = Math.Max(1, GetSettingValue<int>(StatusSetting.Interval));
        if (DateTime.UtcNow - _lastSwitch < TimeSpan.FromSeconds(interval)) return;

        var candidates = Candidates(rows);
        if (candidates.Count == 0) return;

        var next = GetSettingValue<bool>(StatusSetting.Random) ? WeightedRandom(candidates) : NextRoundRobin(candidates, active, forward: true);
        SwitchTo(next, rows, announce: true);
    }

    protected override void OnRegisteredParameterReceived(RegisteredParameter parameter)
    {
        var rows = Rows();
        if (rows.Count == 0) return;
        var active = ActiveRow(rows);

        switch (parameter.Lookup)
        {
            case StatusParameter.Next when parameter.GetValue<bool>():
                {
                    var pool = Candidates(rows);
                    if (pool.Count == 0) pool = rows;
                    SwitchTo(NextRoundRobin(pool, active, forward: true), rows, announce: true);
                    break;
                }

            case StatusParameter.Previous when parameter.GetValue<bool>():
                {
                    var pool = Candidates(rows);
                    if (pool.Count == 0) pool = rows;
                    SwitchTo(NextRoundRobin(pool, active, forward: false), rows, announce: true);
                    break;
                }

            case StatusParameter.Cycle:
                _cycling = parameter.GetValue<bool>();
                _lastSwitch = DateTime.UtcNow;
                break;

            case StatusParameter.Index:
                {
                    var index = parameter.GetValue<int>();
                    if (index >= 0 && index < rows.Count && rows[index] != active)
                        SwitchTo(rows[index], rows, announce: true);
                    break;
                }
        }
    }

    // ─────────────────────────── Selection ───────────────────────────

    private List<StatusEntry> Rows()
        => (GetSettingValue<List<StatusEntry>>(StatusSetting.Statuses) ?? new List<StatusEntry>())
            .Where(r => !string.IsNullOrWhiteSpace(r.Text.Value))
            .ToList();

    private StatusEntry? ActiveRow(List<StatusEntry> rows)
        => rows.FirstOrDefault(r => r.Text.Value == ActiveText);

    private List<StatusEntry> Candidates(List<StatusEntry> rows)
    {
        var overrideGroup = (GetSettingValue<string>(StatusSetting.OverrideGroup) ?? string.Empty).Trim();
        var disabled = (GetSettingValue<List<string>>(StatusSetting.DisabledGroups) ?? new List<string>())
            .Select(g => g.Trim())
            .Where(g => g.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return rows.Where(r =>
        {
            if (!r.UseInCycle.Value) return false;
            var group = r.Group.Value.Trim();
            if (overrideGroup.Length > 0) return string.Equals(group, overrideGroup, StringComparison.OrdinalIgnoreCase);
            return !disabled.Contains(group);
        }).ToList();
    }

    private static StatusEntry NextRoundRobin(List<StatusEntry> pool, StatusEntry? active, bool forward)
    {
        var index = active is null ? -1 : pool.IndexOf(active);
        if (index < 0) return forward ? pool[0] : pool[^1];
        var step = forward ? 1 : pool.Count - 1;
        return pool[(index + step) % pool.Count];
    }

    /// <summary>MagicChatbox's rule: weight = seconds since last shown × random, highest wins.</summary>
    private StatusEntry WeightedRandom(List<StatusEntry> pool)
    {
        var now = DateTime.UtcNow;
        StatusEntry? best = null;
        var bestWeight = double.NegativeInfinity;

        foreach (var row in pool)
        {
            var since = LastUsed.TryGetValue(row.Text.Value, out var last) ? (now - last).TotalSeconds : 1e6;
            var weight = Math.Max(1d, since) * _random.NextDouble();
            if (weight > bestWeight) { bestWeight = weight; best = row; }
        }

        return best ?? pool[0];
    }

    private void SwitchTo(StatusEntry row, List<StatusEntry> rows, bool announce)
    {
        ActiveText = row.Text.Value;
        LastUsed[row.Text.Value] = DateTime.UtcNow;
        _lastSwitch = DateTime.UtcNow;
        _currentIcon = PickIcon(first: false);

        // Forget texts that no longer exist so the persisted map does not grow forever.
        var live = rows.Select(r => r.Text.Value).ToHashSet();
        foreach (var stale in LastUsed.Keys.Where(k => !live.Contains(k)).ToList()) LastUsed.Remove(stale);

        ApplyActive(row, rows);
        if (announce) TriggerEvent(StatusEvent.Changed);
    }

    private void ApplyActive(StatusEntry? row, List<StatusEntry> rows)
    {
        if (row is null)
        {
            _hasActive = false;
            SetVariableValue(StatusVariable.Status, string.Empty);
            SetVariableValue(StatusVariable.Text, string.Empty);
            SetVariableValue(StatusVariable.Icon, string.Empty);
            SetVariableValue(StatusVariable.Index, -1);
            SetVariableValue(StatusVariable.Count, rows.Count);
            SetVariableValue(StatusVariable.Group, string.Empty);
            SendParameter(StatusParameter.Index, -1);
            ChangeState(StatusState.Idle);
            return;
        }

        var text = row.Text.Value;
        var prefixed = GetSettingValue<bool>(StatusSetting.PrefixIcon) && _currentIcon.Length > 0 ? $"{_currentIcon} {text}" : text;

        SetVariableValue(StatusVariable.Status, prefixed);
        SetVariableValue(StatusVariable.Text, text);
        SetVariableValue(StatusVariable.Icon, _currentIcon);
        SetVariableValue(StatusVariable.Index, rows.IndexOf(row));
        SetVariableValue(StatusVariable.Count, rows.Count);
        SetVariableValue(StatusVariable.Group, row.Group.Value);
        SendParameter(StatusParameter.Index, rows.IndexOf(row));

        if (!_hasActive)
        {
            _hasActive = true;
            ChangeState(StatusState.Default);
        }
    }

    private string PickIcon(bool first)
    {
        var icons = (GetSettingValue<List<string>>(StatusSetting.Icons) ?? new List<string>())
            .Select(i => i.Trim())
            .Where(i => i.Length > 0)
            .ToList();
        if (icons.Count == 0) return string.Empty;

        if (GetSettingValue<bool>(StatusSetting.ShuffleIcons)) return icons[_random.Next(icons.Count)];

        if (!first) _iconIndex++;
        _iconIndex %= icons.Count;
        return icons[_iconIndex];
    }

    private enum StatusSetting { Statuses, Cycle, Interval, Random, DisabledGroups, OverrideGroup, PrefixIcon, Icons, ShuffleIcons }

    private enum StatusParameter { Next, Previous, Cycle, Index }

    private enum StatusVariable { Status, Text, Icon, Index, Count, Group }

    private enum StatusState { Default, Idle }

    private enum StatusEvent { Changed }
}
