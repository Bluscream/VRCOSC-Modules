// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// MagicChatbox parity for the speech placeholders: the recognised transcript as spoken
// (speech_text), that transcript translated into a target language (translation), and,
// optionally, other players' speech from a monitor/loopback capture device translated into
// the user's language (twoway_input / twoway_output).
//
// The transcript comes straight from VRCOSC's speech engine: the app hands every partial and
// final result to each running ISpeechHandler module, the same path the official Speech To
// Text module uses. No patching is needed for that. The app's own "translate" switch is not
// a post-processing step that could be intercepted: it builds the Whisper processor in
// translate-to-English mode, so with it on Whisper never produces the source-language text
// at all. In that case speech_text is already English and is reported as such once.

using System.IO;
using VRCOSC.App.Audio;
using VRCOSC.App.SDK.Handlers;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.Settings;

namespace Bluscream.Modules.TranslationPatches;

[ModuleTitle("Speech Translation")]
[ModuleDescription("Your recognised speech as spoken plus its translation into a target language, and optionally other players' speech from a loopback device translated into yours (MagicChatbox speech placeholder parity)")]
[ModuleType(ModuleType.Generic)]
public sealed class TranslationPatchesModule : Module, ISpeechHandler
{
    private const string DefaultLanguage = "en";

    private readonly object _sync = new();
    private Translator? _translator;
    private SecondaryWhisperCapture? _twoWay;

    private DateTime _lastSpoken = DateTime.MinValue;
    private bool _speaking;
    private bool _loggedAppTranslate;

    // Own speech: one translation request in flight, the newest text waiting behind it.
    private string _currentText = string.Empty;
    private string? _pendingText;
    private bool _pendingFinal;
    private bool _translating;

    // Other players' speech (two-way).
    private string _twoWayPending = string.Empty;
    private bool _twoWayTranslating;

    protected override void OnPreLoad()
    {
        CreateTextBox(TranslationSetting.TargetLanguage, "Target language", "ISO 639-1 code your speech is translated into for the translation variable (en, de, ja, ...).", DefaultLanguage);
        CreateSlider(TranslationSetting.SpeakingSeconds, "Speaking window (seconds)", "How long after the last recognised text the Speaking state stays active.", 10, 1, 60, 1);

        CreateToggle(TranslationSetting.TwoWayEnabled, "Two-way translation",
            "Transcribe a second capture device (a loopback/monitor of your headphones, or a virtual cable) with another Whisper instance and translate what other players say into your language.", false);
        CreateTextBox(TranslationSetting.TwoWayDevice, "Two-way capture device",
            "Part of the capture device name to transcribe for two-way translation, e.g. \"Monitor\". The available names are logged when the module starts with two-way enabled.", "Monitor");
        CreateTextBox(TranslationSetting.TwoWayLanguage, "Two-way output language", "ISO 639-1 code other players' speech is translated into.", DefaultLanguage);

        CreateGroup("Two-way", "Other players' speech, captured from a loopback device", TranslationSetting.TwoWayEnabled, TranslationSetting.TwoWayDevice, TranslationSetting.TwoWayLanguage);
    }

    protected override void OnPostLoad()
    {
        var speechText = CreateVariable<string>(TranslationVariable.speech_text, "Speech Text (as spoken)")!;
        var translation = CreateVariable<string>(TranslationVariable.translation, "Translation")!;
        CreateVariable<string>(TranslationVariable.translation_language, "Translation Language");
        CreateVariable<string>(TranslationVariable.twoway_input, "Two-way Input (others, as spoken)");
        var twoWayOutput = CreateVariable<string>(TranslationVariable.twoway_output, "Two-way Output (others, translated)")!;

        CreateState(TranslationState.Idle, "Idle", string.Empty);
        CreateState(TranslationState.Speaking, "Speaking", "{0}\n{1}", new[] { speechText, translation });

        CreateEvent(TranslationEvent.Spoken, "Spoken", "{0}", new[] { speechText }, true, 10f);
        CreateEvent(TranslationEvent.Translated, "Translated", "{0}", new[] { translation }, true, 10f);
        CreateEvent(TranslationEvent.TwoWay, "Two-way Translated", "{0}", new[] { twoWayOutput }, false, 10f);
    }

    protected override Task<bool> OnModuleStart()
    {
        _translator ??= new Translator(Log);
        _lastSpoken = DateTime.MinValue;
        _speaking = false;
        _loggedAppTranslate = false;
        _currentText = string.Empty;
        _pendingText = null;
        _translating = false;
        _twoWayPending = string.Empty;
        _twoWayTranslating = false;

        SetVariableValue(TranslationVariable.speech_text, string.Empty);
        SetVariableValue(TranslationVariable.translation, string.Empty);
        SetVariableValue(TranslationVariable.translation_language, TargetLanguage());
        SetVariableValue(TranslationVariable.twoway_input, string.Empty);
        SetVariableValue(TranslationVariable.twoway_output, string.Empty);
        ChangeState(TranslationState.Idle);

        if (!SettingsManager.GetInstance().GetValue<bool>(VRCOSCSetting.SpeechEnabled))
            Log("The speech engine is disabled in VRCOSC's settings; no text will arrive until it is enabled.");

        StartTwoWay();
        return Task.FromResult(true);
    }

    protected override Task OnModuleStop()
    {
        _twoWay?.Stop();
        ChangeState(TranslationState.Idle);
        return Task.CompletedTask;
    }

    // ─────────────────────────── Own speech ───────────────────────────

    public void OnPartialSpeechResult(string text) => HandleSpeech(text, final: false);

    public void OnFinalSpeechResult(string text) => HandleSpeech(text, final: true);

    private void HandleSpeech(string text, bool final)
    {
        text = text.Trim();
        if (text.Length == 0) return;

        if (SettingsManager.GetInstance().GetValue<bool>(VRCOSCSetting.SpeechTranslate) && !_loggedAppTranslate)
        {
            _loggedAppTranslate = true;
            Log("VRCOSC's own speech translation is on: Whisper outputs English directly, so speech_text is English rather than the language spoken.");
        }

        _lastSpoken = DateTime.UtcNow;
        _currentText = text;
        SetVariableValue(TranslationVariable.speech_text, text);
        SetVariableValue(TranslationVariable.translation_language, TargetLanguage());

        if (!_speaking)
        {
            _speaking = true;
            ChangeState(TranslationState.Speaking);
        }

        if (final) TriggerEvent(TranslationEvent.Spoken);
        QueueTranslation(text, final);
    }

    private void QueueTranslation(string text, bool final)
    {
        lock (_sync)
        {
            if (_translating)
            {
                _pendingText = text;
                _pendingFinal = final;
                return;
            }

            _translating = true;
        }

        _ = TranslateLoopAsync(text, final);
    }

    private async Task TranslateLoopAsync(string text, bool final)
    {
        try
        {
            while (true)
            {
                var language = TargetLanguage();
                var appTranslated = SettingsManager.GetInstance().GetValue<bool>(VRCOSCSetting.SpeechTranslate);
                var translated = appTranslated && language == DefaultLanguage ? text : await _translator!.TranslateAsync(text, language).ConfigureAwait(false);

                // Only publish if the user has not said something newer in the meantime.
                if (text == _currentText)
                {
                    SetVariableValue(TranslationVariable.translation, translated);
                    if (final && translated.Length > 0) TriggerEvent(TranslationEvent.Translated);
                }

                lock (_sync)
                {
                    if (_pendingText is null)
                    {
                        _translating = false;
                        return;
                    }

                    text = _pendingText;
                    final = _pendingFinal;
                    _pendingText = null;
                }
            }
        }
        catch (Exception ex)
        {
            // Background thread: an escaped exception here would terminate VRCOSC.
            Log($"Translation failed unexpectedly: {ex.GetType().Name}: {ex.Message}");
            lock (_sync) { _translating = false; _pendingText = null; }
        }
    }

    [ModuleUpdate(ModuleUpdateMode.Custom, true, 1000)]
    private void UpdateState()
    {
        if (!_speaking) return;

        var window = TimeSpan.FromSeconds(Math.Max(1, GetSettingValue<int>(TranslationSetting.SpeakingSeconds)));
        if (DateTime.UtcNow - _lastSpoken < window) return;

        _speaking = false;
        ChangeState(TranslationState.Idle);
    }

    // ─────────────────────────── Two-way ───────────────────────────

    private void StartTwoWay()
    {
        if (!GetSettingValue<bool>(TranslationSetting.TwoWayEnabled)) return;

        if (!SpeechModelAvailable())
        {
            Log("Two-way: the speech engine is disabled or no Whisper model is set up in VRCOSC's speech settings; two-way translation stays off.");
            return;
        }

        var filter = (GetSettingValue<string>(TranslationSetting.TwoWayDevice) ?? string.Empty).Trim();
        if (filter.Length == 0)
        {
            Log($"Two-way: no capture device filter set. Available: {string.Join(" | ", SecondaryWhisperCapture.CaptureDevices().Select(d => d.Name))}");
            return;
        }

        _twoWay ??= new SecondaryWhisperCapture(Log);
        _twoWay.Start(filter);
    }

    private static bool SpeechModelAvailable()
    {
        var settings = SettingsManager.GetInstance();
        if (!settings.GetValue<bool>(VRCOSCSetting.SpeechEnabled)) return false;

        // The built-in models live in the app's private storage (AppManager is internal, so the
        // path cannot be asked for); the app's AudioProcessor resolves and validates them itself
        // and logs when one is missing. Only a custom model path can be checked here.
        return settings.GetValue<SpeechModel>(VRCOSCSetting.SpeechModel) switch
        {
            SpeechModel.Custom => File.Exists(settings.GetValue<string>(VRCOSCSetting.SpeechModelPath)),
            _ => true
        };
    }

    // Same cadence as the app's own engine ("Do not change this from 1.5").
    [ModuleUpdate(ModuleUpdateMode.Custom, true, 1500)]
    private void UpdateTwoWay()
    {
        if (_twoWay is not { IsRunning: true }) return;
        _ = PollTwoWayAsync();
    }

    private async Task PollTwoWayAsync()
    {
        try
        {
            var result = await _twoWay!.PollAsync().ConfigureAwait(false);
            if (result is null) return;

            // Same filters the app applies to its own results.
            var text = result.Text.Trim();
            if (text.Length == 0 || text.StartsWith('[')) return;
            if (result.Confidence < SettingsManager.GetInstance().GetValue<float>(VRCOSCSetting.SpeechConfidence)) return;

            SetVariableValue(TranslationVariable.twoway_input, text);

            lock (_sync)
            {
                _twoWayPending = text;
                if (_twoWayTranslating) return;
                _twoWayTranslating = true;
            }

            while (true)
            {
                string current;
                lock (_sync) { current = _twoWayPending; _twoWayPending = string.Empty; }

                var translated = await _translator!.TranslateAsync(current, Language(TranslationSetting.TwoWayLanguage)).ConfigureAwait(false);
                SetVariableValue(TranslationVariable.twoway_output, translated);
                if (result.IsFinal && translated.Length > 0) TriggerEvent(TranslationEvent.TwoWay);

                lock (_sync)
                {
                    if (_twoWayPending.Length == 0)
                    {
                        _twoWayTranslating = false;
                        return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Two-way failed unexpectedly: {ex.GetType().Name}: {ex.Message}");
            lock (_sync) { _twoWayTranslating = false; }
        }
    }

    // ─────────────────────────── Helpers ───────────────────────────

    private string TargetLanguage() => Language(TranslationSetting.TargetLanguage);

    private string Language(TranslationSetting setting)
    {
        var language = (GetSettingValue<string>(setting) ?? string.Empty).Trim().ToLowerInvariant();
        return language.Length > 0 ? language : DefaultLanguage;
    }

    private enum TranslationSetting { TargetLanguage, SpeakingSeconds, TwoWayEnabled, TwoWayDevice, TwoWayLanguage }

    // Spelled as the MagicChatbox placeholder keys on purpose (the lookup is the lower-cased name).
    private enum TranslationVariable { speech_text, translation, translation_language, twoway_input, twoway_output }

    private enum TranslationState { Idle, Speaking }

    private enum TranslationEvent { Spoken, Translated, TwoWay }
}
