// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Bluscream.Modules.Utilities;
using VRCOSC.App.SDK.Modules;

namespace Bluscream.Modules.LinuxAudioFx;

/// <summary>
/// Linux counterpart of MagicChatbox's Soundpad and Voicemod integrations. Soundboard
/// playback is detected from PipeWire output streams, the "voice" is the active EasyEffects
/// input preset (or a PipeWire filter-chain node). All host access goes through the
/// embedded <c>vrcosc_audiofx.sh</c> script and the Wine bash bridge.
/// </summary>
[ModuleTitle("Linux Audio FX")]
[ModuleDescription("Soundboard (Soundux, Kenku, ...) and voice changer (EasyEffects, PipeWire filters) state on Linux hosts — MagicChatbox soundpad_* / voicemod_* counterparts")]
[ModuleType(ModuleType.Integrations)]
public sealed class LinuxAudioFxModule : Module
{
    private const string ScriptName = "vrcosc_audiofx.sh";
    private const string OutputFileName = ".vrcosc_audiofx.json";
    private const int TickMs = 250;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private DateTime _lastPoll = DateTime.MinValue;
    private DateTime _lastSoundStopped = DateTime.MinValue;
    private string _currentSound = string.Empty;
    private string _heldSound = string.Empty;
    private bool _toolsLogged;
    private bool _missingOutputLogged;

    protected override void OnPreLoad()
    {
        CreateSlider(AudioFxSetting.PollIntervalMs, "Poll interval (ms)", "How often the host is asked for the PipeWire / EasyEffects state.", 1000, 250, 10000, 250);
        CreateTextBox(AudioFxSetting.SoundboardApps, "Soundboard apps", "Pipe-separated, case-insensitive list of application names (regex prefixes) whose output streams count as soundboard playback. Requires a module restart.", "Soundux|Kenku|Sound Board|Soundboard|Ducky|Zedd");
        CreateToggle(AudioFxSetting.MatchRoleHint, "Match media.role hint", "Also treat any stream whose media.role looks like \"Sound Board\" as soundboard playback. Requires a module restart.", true);
        CreateSlider(AudioFxSetting.HoldSeconds, "Hold last sound (s)", "How long voicemod_sound keeps the last soundboard sound name after it stopped playing.", 5, 1, 60);
    }

    protected override void OnPostLoad()
    {
        var sound = CreateVariable<string>(AudioFxVariable.soundpad_sound, "Soundboard sound (soundpad_sound)")!;
        var app = CreateVariable<string>(AudioFxVariable.soundpad_app, "Soundboard app (soundpad_app)")!;
        var voice = CreateVariable<string>(AudioFxVariable.voicemod_voice, "Voice preset (voicemod_voice)")!;
        CreateVariable<string>(AudioFxVariable.voicemod_sound, "Last sound, held (voicemod_sound)");

        CreateState(AudioFxState.Playing, "Playing", "🔊 {0} ({1})\n🎙 {2}", new[] { sound, app, voice });
        CreateState(AudioFxState.Idle, "Idle", "🎙 {0}", new[] { voice });
        CreateEvent(AudioFxEvent.SoundStarted, "Sound started", "🔊 {0}", new[] { sound });
    }

    protected override Task<bool> OnModuleStart()
    {
        DeployHelperScript();
        _lastPoll = DateTime.MinValue;
        _currentSound = string.Empty;
        _heldSound = string.Empty;
        _toolsLogged = false;
        _missingOutputLogged = false;
        ChangeState(AudioFxState.Idle);
        return Task.FromResult(true);
    }

    protected override Task OnModuleStop() => Task.CompletedTask;

    private void DeployHelperScript()
    {
        try
        {
            using var stream = typeof(LinuxAudioFxModule).Assembly.GetManifestResourceStream($"Bluscream.Modules.LinuxAudioFx.{ScriptName}");
            if (stream is null)
            {
                Log($"Error: embedded resource {ScriptName} not found.");
                return;
            }

            using var reader = new StreamReader(stream);
            var apps = (GetSettingValue<string>(AudioFxSetting.SoundboardApps) ?? string.Empty).Replace("\"", string.Empty).Replace("\\", string.Empty);
            var roleHint = GetSettingValue<bool>(AudioFxSetting.MatchRoleHint) ? "Sound ?Board" : string.Empty;
            var scriptContent = reader.ReadToEnd()
                .Replace("SOUNDBOARD_APPS=\"Soundux|Kenku|Sound Board|Soundboard|Ducky|Zedd\"", $"SOUNDBOARD_APPS=\"{apps}\"")
                .Replace("ROLE_HINT=\"Sound ?Board\"", $"ROLE_HINT=\"{roleHint}\"");

            var wineTargetPath = Path.Combine(LinuxUtils.GetWineHomeDir(), ".local", "bin", ScriptName);
            var dir = Path.GetDirectoryName(wineTargetPath);
            if (dir is not null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(wineTargetPath, scriptContent);
            LinuxUtils.ChmodPlusX($"$HOME/.local/bin/{ScriptName}", ex => Log($"Error making script executable: {ex.Message}"));
            Log($"Helper script deployed to ~/.local/bin/{ScriptName} (apps={apps}, roleHint={(roleHint.Length > 0 ? "on" : "off")})");
        }
        catch (Exception ex)
        {
            Log($"Error deploying helper script: {ex.Message}");
        }
    }

    [ModuleUpdate(ModuleUpdateMode.Custom, true, TickMs)]
    private void Update()
    {
        if (!ModuleUtils.IsStarted()) return;

        var now = DateTime.UtcNow;
        var interval = TimeSpan.FromMilliseconds(GetSettingValue<int>(AudioFxSetting.PollIntervalMs));
        if (now - _lastPoll >= interval)
        {
            _lastPoll = now;
            Poll();
        }

        // Hold timer runs on every tick so the held name clears on time regardless of poll rate.
        if (_currentSound.Length == 0 && _heldSound.Length > 0 &&
            now - _lastSoundStopped >= TimeSpan.FromSeconds(GetSettingValue<int>(AudioFxSetting.HoldSeconds)))
        {
            _heldSound = string.Empty;
            SetVariableValue(AudioFxVariable.voicemod_sound, string.Empty);
        }
    }

    private void Poll()
    {
        try
        {
            LinuxUtils.RunHostScript(ScriptName, null, ex => Log($"Error running {ScriptName}: {ex.Message}"));

            var outputPath = Path.Combine(LinuxUtils.GetWineHomeDir(), OutputFileName);
            if (!File.Exists(outputPath))
            {
                if (!_missingOutputLogged)
                {
                    _missingOutputLogged = true;
                    Log($"{OutputFileName} not found yet — the helper script has not produced output (is bash reachable through the Wine bridge?)");
                }
                return;
            }

            var report = JsonSerializer.Deserialize<AudioFxReport>(File.ReadAllText(outputPath), JsonOptions);
            if (report is null) return;

            LogToolsOnce(report.Tools);
            ApplySoundboard(report.Soundboard);
            ApplyVoice(report.Voice);
        }
        catch (JsonException ex)
        {
            Log($"Could not parse {OutputFileName}: {ex.Message}");
        }
        catch (Exception ex)
        {
            Log($"Error polling audio FX state: {ex.Message}");
        }
    }

    private void ApplySoundboard(SoundboardInfo? sb)
    {
        var sound = sb?.Playing == true ? sb.Sound ?? string.Empty : string.Empty;
        var app = sb?.Playing == true ? sb.App ?? string.Empty : string.Empty;

        if (sound.Length > 0)
        {
            if (sound != _currentSound)
            {
                _heldSound = sound;
                SetVariableValue(AudioFxVariable.soundpad_sound, sound);
                SetVariableValue(AudioFxVariable.soundpad_app, app);
                SetVariableValue(AudioFxVariable.voicemod_sound, sound);
                ChangeState(AudioFxState.Playing);
                TriggerEvent(AudioFxEvent.SoundStarted);
            }
        }
        else if (_currentSound.Length > 0)
        {
            _lastSoundStopped = DateTime.UtcNow;
            SetVariableValue(AudioFxVariable.soundpad_sound, string.Empty);
            SetVariableValue(AudioFxVariable.soundpad_app, string.Empty);
            ChangeState(AudioFxState.Idle);
        }

        _currentSound = sound;
    }

    private void ApplyVoice(VoiceInfo? voice)
    {
        var name = "None";
        if (voice is not null)
        {
            if (voice.EasyEffectsRunning && !string.IsNullOrWhiteSpace(voice.InputPreset))
                name = voice.InputPreset;
            else if (voice.Filters is { Count: > 0 })
                name = voice.Filters[0];
        }

        SetVariableValue(AudioFxVariable.voicemod_voice, name);
    }

    private void LogToolsOnce(Dictionary<string, bool>? tools)
    {
        if (_toolsLogged || tools is null) return;
        _toolsLogged = true;

        var missing = tools.Where(t => !t.Value).Select(t => t.Key).ToList();
        if (missing.Count == 0) return;

        var hints = new List<string>();
        if (missing.Contains("pw-dump") && missing.Contains("pactl")) hints.Add("no PipeWire/PulseAudio CLI — soundboard detection disabled");
        else if (missing.Contains("jq") && missing.Contains("pactl")) hints.Add("jq missing and no pactl fallback — soundboard detection disabled");
        else if (missing.Contains("jq")) hints.Add("jq missing — using pactl fallback, PipeWire filter nodes not detected");
        if (missing.Contains("gsettings")) hints.Add("gsettings missing — EasyEffects preset only readable from the Flatpak keyfile");
        Log($"Host tools missing: {string.Join(", ", missing)}. {string.Join("; ", hints)}");
    }

    // ---------------------------------------------------------------------------
    // JSON contract with vrcosc_audiofx.sh
    // ---------------------------------------------------------------------------

    private sealed class AudioFxReport
    {
        public SoundboardInfo? Soundboard { get; set; }
        public VoiceInfo? Voice { get; set; }
        public Dictionary<string, bool>? Tools { get; set; }
    }

    private sealed class SoundboardInfo
    {
        public bool Playing { get; set; }
        public string? App { get; set; }
        public string? Sound { get; set; }
    }

    private sealed class VoiceInfo
    {
        [System.Text.Json.Serialization.JsonPropertyName("easyeffects_running")]
        public bool EasyEffectsRunning { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("input_preset")]
        public string? InputPreset { get; set; }

        public List<string>? Filters { get; set; }
    }

    private enum AudioFxSetting
    {
        PollIntervalMs,
        SoundboardApps,
        MatchRoleHint,
        HoldSeconds
    }

    // Enum names are lowercased by the SDK to form the ChatBox lookup key; these must match
    // the MagicChatbox placeholders exactly, hence the snake_case.
    private enum AudioFxVariable
    {
        soundpad_sound,
        soundpad_app,
        voicemod_voice,
        voicemod_sound
    }

    private enum AudioFxState
    {
        Playing,
        Idle
    }

    private enum AudioFxEvent
    {
        SoundStarted
    }
}
