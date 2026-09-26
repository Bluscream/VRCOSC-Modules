// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Heart rate with session min/max and a trend arrow as ChatBox variables (MagicChatbox
// parity). Modules cannot read each other's variables, so this cannot sit on top of the
// official Pulsoid/HypeRate modules; it speaks the same protocols itself and mirrors the
// official output parameters so existing avatar setups keep working.

using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;
using VRCOSC.App.Utils;

namespace Bluscream.Modules.HeartrateStats;

[ModuleTitle("Heartrate Stats")]
[ModuleDescription("Heart rate from Pulsoid, HypeRate or any OSC parameter, with session min/max and a trend arrow as ChatBox variables. Sends the same avatar parameters as the official heartrate modules.")]
[ModuleType(ModuleType.Health)]
public class HeartrateStatsModule : Module
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(30);

    private readonly HeartrateSession _session = new();
    private IHeartrateSource? _source;
    private CancellationTokenSource? _beatCts;
    private Task? _beatTask;
    private bool _beatValue;
    private bool _wasReceiving;
    private bool _pendingConnectEvent;
    private bool _pendingDisconnectEvent;

    protected override void OnPreLoad()
    {
        CreateDropdown(HeartrateStatsSetting.Provider, "Provider", "Where the heart rate comes from", HeartrateProvider.Pulsoid);
        CreateTextBox(HeartrateStatsSetting.PulsoidToken, "Pulsoid Access Token", "Token from https://pulsoid.net/ui/keys (needs the Data:Heart Rate:Read scope)", string.Empty);
        CreateTextBox(HeartrateStatsSetting.HypeRateId, "HypeRate Session ID", "The ID shown in the HypeRate app (the characters at the end of your app.hyperate.io link)", string.Empty);
        CreateTextBox(HeartrateStatsSetting.HypeRateApiKey, "HypeRate API Key", "Application key issued by HypeRate (request one at https://www.hyperate.io/api)", string.Empty);
        CreateTextBox(HeartrateStatsSetting.OscAddress, "OSC Input Parameter", "Parameter name another tool writes the heart rate to (int or float bpm), e.g. HR or VRCOSC/HeartrateStats/Input", "VRCOSC/HeartrateStats/Input");

        CreateSlider(HeartrateStatsSetting.TrendWindowSeconds, "Trend Window (s)", "How far back the trend arrow looks", 30, 5, 300, 5);
        CreateSlider(HeartrateStatsSetting.TrendThreshold, "Trend Threshold (bpm)", "Minimum change across the window before the arrow leaves flat", 3, 1, 30);
        CreateTextBox(HeartrateStatsSetting.AveragePeriodSeconds, "Average Period (s)", "Period used for the Average parameter and variable", 10);
        CreateToggle(HeartrateStatsSetting.ResetNow, "Reset Min/Max", "Flip this toggle (either way) to reset the session minimum and maximum", false);

        CreateTextBox(HeartrateStatsSetting.NormalisedLowerbound, "Normalised Lowerbound", "The bpm mapped to 0 on the Normalised parameter", 0);
        CreateTextBox(HeartrateStatsSetting.NormalisedUpperbound, "Normalised Upperbound", "The bpm mapped to 1 on the Normalised parameter", 240);
        CreateToggle(HeartrateStatsSetting.BeatMode, "Beat Mode", "Whether the Beat parameter toggles its value (off) or becomes true for one update (on) on every beat", false);

        CreateGroup("Source", string.Empty, HeartrateStatsSetting.Provider, HeartrateStatsSetting.PulsoidToken, HeartrateStatsSetting.HypeRateId, HeartrateStatsSetting.HypeRateApiKey, HeartrateStatsSetting.OscAddress);
        CreateGroup("Statistics", string.Empty, HeartrateStatsSetting.TrendWindowSeconds, HeartrateStatsSetting.TrendThreshold, HeartrateStatsSetting.AveragePeriodSeconds, HeartrateStatsSetting.ResetNow);
        CreateGroup("Parameters", string.Empty, HeartrateStatsSetting.NormalisedLowerbound, HeartrateStatsSetting.NormalisedUpperbound, HeartrateStatsSetting.BeatMode);

        // Same names as the official HeartrateModule so avatar prefabs keep working.
        RegisterParameter<bool>(HeartrateStatsParameter.Connected, "VRCOSC/Heartrate/Connected", ParameterMode.Write, "Connected", "Whether this module is connected and receiving values");
        RegisterParameter<int>(HeartrateStatsParameter.Value, "VRCOSC/Heartrate/Value", ParameterMode.Write, "Value", "The value of your heartrate");
        RegisterParameter<float>(HeartrateStatsParameter.Normalised, "VRCOSC/Heartrate/Normalised", ParameterMode.Write, "Normalised", "The heartrate value normalised from the set bounds to 0-1");
        RegisterParameter<int>(HeartrateStatsParameter.Average, "VRCOSC/Heartrate/Average", ParameterMode.Write, "Average", "The average of your heartrate");
        RegisterParameter<bool>(HeartrateStatsParameter.Beat, "VRCOSC/Heartrate/Beat", ParameterMode.ReadWrite, "Beat", "Toggles value OR becomes true for 1 update (depending on the setting) when your heart beats");
        RegisterParameter<bool>(HeartrateStatsParameter.LegacyEnabled, "VRCOSC/Heartrate/Enabled", ParameterMode.Write, "Enabled", "Whether this module is connected and receiving values", true);
        RegisterParameter<float>(HeartrateStatsParameter.LegacyUnits, "VRCOSC/Heartrate/Units", ParameterMode.Write, "Units", "The units digit 0-9 mapped to a float", true);
        RegisterParameter<float>(HeartrateStatsParameter.LegacyTens, "VRCOSC/Heartrate/Tens", ParameterMode.Write, "Tens", "The tens digit 0-9 mapped to a float", true);
        RegisterParameter<float>(HeartrateStatsParameter.LegacyHundreds, "VRCOSC/Heartrate/Hundreds", ParameterMode.Write, "Hundreds", "The hundreds digit 0-9 mapped to a float", true);

        RegisterParameter<int>(HeartrateStatsParameter.Min, "VRCOSC/Heartrate/Min", ParameterMode.Write, "Session Min", "Lowest heartrate this session");
        RegisterParameter<int>(HeartrateStatsParameter.Max, "VRCOSC/Heartrate/Max", ParameterMode.Write, "Session Max", "Highest heartrate this session");
        RegisterParameter<int>(HeartrateStatsParameter.Trend, "VRCOSC/Heartrate/Trend", ParameterMode.Write, "Trend", "-1 falling, 0 flat, 1 rising");
        RegisterParameter<bool>(HeartrateStatsParameter.ResetStats, "VRCOSC/Heartrate/ResetStats", ParameterMode.Read, "Reset Stats", "Becoming true resets the session minimum and maximum");
    }

    protected override void OnPostLoad()
    {
        var current = CreateVariable<int>(HeartrateStatsVariable.Heartrate, "Heartrate")!;
        var min = CreateVariable<int>(HeartrateStatsVariable.Heartrate_Min, "Session Min")!;
        var max = CreateVariable<int>(HeartrateStatsVariable.Heartrate_Max, "Session Max")!;
        var trend = CreateVariable<string>(HeartrateStatsVariable.Heartrate_Trend, "Trend Arrow")!;
        CreateVariable<int>(HeartrateStatsVariable.Heartrate_Average, "Average");
        CreateVariable<bool>(HeartrateStatsVariable.Heartrate_Connected, "Connected");

        CreateState(HeartrateStatsState.Connected, "Connected", "❤ {0} {3} ({1}-{2})", new[] { current, min, max, trend });
        CreateState(HeartrateStatsState.Disconnected, "Disconnected", string.Empty);

        CreateEvent(HeartrateStatsEvent.Connected, "Connected", "❤ Heartrate connected");
        CreateEvent(HeartrateStatsEvent.Disconnected, "Disconnected", "❤ Heartrate disconnected");

        GetSetting(HeartrateStatsSetting.ResetNow).OnSettingChange += () =>
        {
            _session.ResetExtremes();
            Log("Session min/max reset");
        };
    }

    protected override Task<bool> OnModuleStart()
    {
        _session.Reset();
        _session.Retention = Retention();
        _wasReceiving = false;
        _pendingConnectEvent = false;
        _pendingDisconnectEvent = false;
        _beatValue = false;

        var provider = GetSettingValue<HeartrateProvider>(HeartrateStatsSetting.Provider);

        try
        {
            _source = CreateSource(provider);
        }
        catch (InvalidOperationException ex)
        {
            Log(ex.Message);
            return Task.FromResult(false);
        }

        if (_source is not null)
        {
            _source.HeartrateReceived += OnHeartrate;
            _source.ConnectionChanged += OnConnectionChanged;
            _source.Start();
        }
        else
        {
            Log($"Listening for heart rate on parameter '{OscAddress()}'");
        }

        _beatCts = new CancellationTokenSource();
        _beatTask = Task.Run(() => BeatLoopAsync(_beatCts.Token));

        ChangeState(HeartrateStatsState.Disconnected);
        return Task.FromResult(true);
    }

    protected override async Task OnModuleStop()
    {
        if (_beatCts is not null)
        {
            await _beatCts.CancelAsync();
            if (_beatTask is not null) await _beatTask;
            _beatCts.Dispose();
            _beatCts = null;
            _beatTask = null;
        }

        if (_source is not null)
        {
            await _source.StopAsync();
            _source.HeartrateReceived -= OnHeartrate;
            _source.ConnectionChanged -= OnConnectionChanged;
            _source = null;
        }

        SendParameter(HeartrateStatsParameter.Connected, false);
        SendParameter(HeartrateStatsParameter.LegacyEnabled, false);
        SetVariableValue(HeartrateStatsVariable.Heartrate_Connected, false);
        ChangeState(HeartrateStatsState.Disconnected);
    }

    /// <summary>Returns null for the OSC provider, which needs no background source.</summary>
    private IHeartrateSource? CreateSource(HeartrateProvider provider)
    {
        switch (provider)
        {
            case HeartrateProvider.Pulsoid:
            {
                var token = GetSettingValue<string>(HeartrateStatsSetting.PulsoidToken)?.Trim();
                if (string.IsNullOrEmpty(token)) throw new InvalidOperationException("Enter your Pulsoid access token in the module settings.");
                return new PulsoidSource(token, Log);
            }

            case HeartrateProvider.HypeRate:
            {
                var id = GetSettingValue<string>(HeartrateStatsSetting.HypeRateId)?.Trim();
                var key = GetSettingValue<string>(HeartrateStatsSetting.HypeRateApiKey)?.Trim();
                if (string.IsNullOrEmpty(id)) throw new InvalidOperationException("Enter your HypeRate session ID in the module settings.");
                if (string.IsNullOrEmpty(key)) throw new InvalidOperationException("Enter a HypeRate API key in the module settings.");
                return new HypeRateSource(id, key, Log);
            }

            case HeartrateProvider.Osc:
                if (string.IsNullOrEmpty(OscAddress())) throw new InvalidOperationException("Enter the OSC input parameter name in the module settings.");
                return null;

            default:
                throw new InvalidOperationException($"Unknown provider {provider}");
        }
    }

    private string OscAddress() => GetSettingValue<string>(HeartrateStatsSetting.OscAddress)?.Trim() ?? string.Empty;

    private TimeSpan Retention()
    {
        var trend = GetSettingValue<int>(HeartrateStatsSetting.TrendWindowSeconds);
        var average = GetSettingValue<int>(HeartrateStatsSetting.AveragePeriodSeconds);
        return TimeSpan.FromSeconds(Math.Max(Math.Max(trend, average), 5));
    }

    private void OnHeartrate(int bpm) => _session.Add(bpm, DateTimeOffset.UtcNow);

    private void OnConnectionChanged(bool connected)
    {
        // Only record; ChatBox state and events are applied on the update thread.
        if (connected) _pendingConnectEvent = true;
        else _pendingDisconnectEvent = true;
    }

    protected override void OnAnyParameterReceived(VRChatParameter parameter)
    {
        if (GetSettingValue<HeartrateProvider>(HeartrateStatsSetting.Provider) != HeartrateProvider.Osc) return;
        if (!string.Equals(parameter.Name, OscAddress(), StringComparison.Ordinal)) return;

        var bpm = parameter.Type switch
        {
            ParameterType.Int => parameter.GetValue<int>(),
            ParameterType.Float => (int)MathF.Round(parameter.GetValue<float>()),
            _ => 0
        };

        _session.Add(bpm, DateTimeOffset.UtcNow);
    }

    protected override void OnRegisteredParameterReceived(RegisteredParameter parameter)
    {
        switch (parameter.Lookup)
        {
            case HeartrateStatsParameter.ResetStats when parameter.GetValue<bool>():
                _session.ResetExtremes();
                Log("Session min/max reset via parameter");
                break;

            case HeartrateStatsParameter.Beat when GetSettingValue<bool>(HeartrateStatsSetting.BeatMode) && parameter.GetValue<bool>():
                // Pulse mode: VRChat echoed our true, so drop it back to false.
                _beatValue = false;
                SendParameter(HeartrateStatsParameter.Beat, false);
                break;
        }
    }

    [ModuleUpdate(ModuleUpdateMode.Custom, true, 1000)]
    private void Update()
    {
        var now = DateTimeOffset.UtcNow;
        var receiving = _session.IsReceiving(ReceiveTimeout, now);

        if (_pendingConnectEvent)
        {
            _pendingConnectEvent = false;
            TriggerEvent(HeartrateStatsEvent.Connected);
        }

        if (_pendingDisconnectEvent)
        {
            _pendingDisconnectEvent = false;
            TriggerEvent(HeartrateStatsEvent.Disconnected);
        }

        if (receiving != _wasReceiving)
        {
            _wasReceiving = receiving;
            ChangeState(receiving ? HeartrateStatsState.Connected : HeartrateStatsState.Disconnected);
            if (!receiving) Log("No heart rate received for a while; marking disconnected");
        }

        var current = receiving ? _session.Current : 0;
        var average = receiving ? _session.Average(TimeSpan.FromSeconds(GetSettingValue<int>(HeartrateStatsSetting.AveragePeriodSeconds)), now) : 0;
        var trend = receiving
            ? _session.Trend(TimeSpan.FromSeconds(GetSettingValue<int>(HeartrateStatsSetting.TrendWindowSeconds)), GetSettingValue<int>(HeartrateStatsSetting.TrendThreshold), now)
            : HeartrateSession.TrendFlat;

        SetVariableValue(HeartrateStatsVariable.Heartrate, current);
        SetVariableValue(HeartrateStatsVariable.Heartrate_Min, _session.Min);
        SetVariableValue(HeartrateStatsVariable.Heartrate_Max, _session.Max);
        SetVariableValue(HeartrateStatsVariable.Heartrate_Trend, trend);
        SetVariableValue(HeartrateStatsVariable.Heartrate_Average, average);
        SetVariableValue(HeartrateStatsVariable.Heartrate_Connected, receiving);

        SendParameters(receiving, current, average, trend);
    }

    private void SendParameters(bool receiving, int current, int average, string trend)
    {
        SendParameter(HeartrateStatsParameter.Connected, receiving);
        SendParameter(HeartrateStatsParameter.LegacyEnabled, receiving);
        SendParameter(HeartrateStatsParameter.Value, current);
        SendParameter(HeartrateStatsParameter.Average, average);
        SendParameter(HeartrateStatsParameter.Min, _session.Min);
        SendParameter(HeartrateStatsParameter.Max, _session.Max);
        SendParameter(HeartrateStatsParameter.Trend, trend == HeartrateSession.TrendUp ? 1 : trend == HeartrateSession.TrendDown ? -1 : 0);

        var lower = GetSettingValue<int>(HeartrateStatsSetting.NormalisedLowerbound);
        var upper = GetSettingValue<int>(HeartrateStatsSetting.NormalisedUpperbound);
        var normalised = receiving && upper > lower ? Math.Clamp(Interpolation.Map((float)current, lower, upper, 0f, 1f), 0f, 1f) : 0f;
        SendParameter(HeartrateStatsParameter.Normalised, normalised);

        var digits = Math.Clamp(current, 0, 999);
        SendParameter(HeartrateStatsParameter.LegacyUnits, digits % 10 / 10f);
        SendParameter(HeartrateStatsParameter.LegacyTens, digits / 10 % 10 / 10f);
        SendParameter(HeartrateStatsParameter.LegacyHundreds, digits / 100 / 10f);
    }

    // Runs on a thread-pool thread: everything that can throw is caught, an unhandled
    // exception here would take the whole VRCOSC process down.
    private async Task BeatLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var bpm = _wasReceiving ? _session.Current : 0;

                if (bpm <= 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), ct);
                    continue;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(60_000d / bpm), ct);

                _beatValue = GetSettingValue<bool>(HeartrateStatsSetting.BeatMode) || !_beatValue;
                SendParameter(HeartrateStatsParameter.Beat, _beatValue);
            }
        }
        catch (OperationCanceledException)
        {
            // module stop
        }
        catch (Exception ex)
        {
            Log($"Beat loop stopped: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private enum HeartrateStatsSetting
    {
        Provider, PulsoidToken, HypeRateId, HypeRateApiKey, OscAddress,
        TrendWindowSeconds, TrendThreshold, AveragePeriodSeconds, ResetNow,
        NormalisedLowerbound, NormalisedUpperbound, BeatMode
    }

    private enum HeartrateStatsParameter
    {
        Connected, Value, Normalised, Average, Beat,
        LegacyEnabled, LegacyUnits, LegacyTens, LegacyHundreds,
        Min, Max, Trend, ResetStats
    }

    // Member names become the ChatBox lookup keys (lowercased): heartrate, heartrate_min, ...
    private enum HeartrateStatsVariable
    {
        Heartrate, Heartrate_Min, Heartrate_Max, Heartrate_Trend, Heartrate_Average, Heartrate_Connected
    }

    private enum HeartrateStatsState { Connected, Disconnected }

    private enum HeartrateStatsEvent { Connected, Disconnected }
}

public enum HeartrateProvider
{
    Pulsoid,
    HypeRate,
    Osc
}
