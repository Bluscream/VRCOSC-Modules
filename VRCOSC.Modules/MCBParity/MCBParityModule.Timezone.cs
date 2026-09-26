// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Timezone part: MagicChatbox prints the local zone as its short abbreviation ("CEST", "PST").
// .NET only knows the long Windows display names, so the abbreviation comes from, in order:
//   1. the TimezoneOverride setting when non-empty
//   2. `date +%Z` on the Linux host (through the Wine bash bridge, or directly when native),
//      refreshed every 10 minutes so DST switches are picked up
//   3. a small table of common IANA zones (TimeZoneInfo.Local mapped to its IANA id)
//   4. "UTC+2" built from the current offset
// The offset variable is always derived from TimeZoneInfo.Local, which under Wine mirrors
// the host's zone.

using System.Globalization;
using System.IO;
using Bluscream.Modules.Utilities;
using VRCOSC.App.SDK.Modules;

namespace Bluscream.Modules.MCBParity;

public sealed partial class MCBParityModule
{
    private const string HostTimezoneFile = ".vrcosc_timezone.txt";
    private static readonly TimeSpan HostTimezoneRefresh = TimeSpan.FromMinutes(10);

    // IANA id -> (standard, daylight) abbreviation; daylight equals standard where the zone has no DST.
    private static readonly Dictionary<string, (string Standard, string Daylight)> IanaAbbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["UTC"] = ("UTC", "UTC"), ["Etc/UTC"] = ("UTC", "UTC"), ["Etc/GMT"] = ("GMT", "GMT"),
        ["Europe/London"] = ("GMT", "BST"), ["Europe/Dublin"] = ("GMT", "IST"), ["Europe/Lisbon"] = ("WET", "WEST"),
        ["Europe/Berlin"] = ("CET", "CEST"), ["Europe/Paris"] = ("CET", "CEST"), ["Europe/Madrid"] = ("CET", "CEST"),
        ["Europe/Rome"] = ("CET", "CEST"), ["Europe/Amsterdam"] = ("CET", "CEST"), ["Europe/Brussels"] = ("CET", "CEST"),
        ["Europe/Vienna"] = ("CET", "CEST"), ["Europe/Zurich"] = ("CET", "CEST"), ["Europe/Stockholm"] = ("CET", "CEST"),
        ["Europe/Oslo"] = ("CET", "CEST"), ["Europe/Copenhagen"] = ("CET", "CEST"), ["Europe/Prague"] = ("CET", "CEST"),
        ["Europe/Warsaw"] = ("CET", "CEST"), ["Europe/Budapest"] = ("CET", "CEST"), ["Europe/Belgrade"] = ("CET", "CEST"),
        ["Europe/Helsinki"] = ("EET", "EEST"), ["Europe/Kiev"] = ("EET", "EEST"), ["Europe/Kyiv"] = ("EET", "EEST"),
        ["Europe/Athens"] = ("EET", "EEST"), ["Europe/Bucharest"] = ("EET", "EEST"), ["Europe/Sofia"] = ("EET", "EEST"),
        ["Europe/Istanbul"] = ("TRT", "TRT"), ["Europe/Moscow"] = ("MSK", "MSK"),
        ["America/New_York"] = ("EST", "EDT"), ["America/Toronto"] = ("EST", "EDT"), ["America/Detroit"] = ("EST", "EDT"),
        ["America/Chicago"] = ("CST", "CDT"), ["America/Mexico_City"] = ("CST", "CST"), ["America/Denver"] = ("MST", "MDT"),
        ["America/Phoenix"] = ("MST", "MST"), ["America/Los_Angeles"] = ("PST", "PDT"), ["America/Vancouver"] = ("PST", "PDT"),
        ["America/Anchorage"] = ("AKST", "AKDT"), ["Pacific/Honolulu"] = ("HST", "HST"), ["America/Halifax"] = ("AST", "ADT"),
        ["America/Sao_Paulo"] = ("BRT", "BRT"), ["America/Argentina/Buenos_Aires"] = ("ART", "ART"), ["America/Bogota"] = ("COT", "COT"),
        ["Asia/Tokyo"] = ("JST", "JST"), ["Asia/Seoul"] = ("KST", "KST"), ["Asia/Shanghai"] = ("CST", "CST"),
        ["Asia/Hong_Kong"] = ("HKT", "HKT"), ["Asia/Taipei"] = ("CST", "CST"), ["Asia/Singapore"] = ("SGT", "SGT"),
        ["Asia/Kolkata"] = ("IST", "IST"), ["Asia/Calcutta"] = ("IST", "IST"), ["Asia/Dubai"] = ("GST", "GST"),
        ["Asia/Bangkok"] = ("ICT", "ICT"), ["Asia/Jakarta"] = ("WIB", "WIB"), ["Asia/Manila"] = ("PHT", "PHT"),
        ["Asia/Jerusalem"] = ("IST", "IDT"), ["Asia/Karachi"] = ("PKT", "PKT"),
        ["Australia/Sydney"] = ("AEST", "AEDT"), ["Australia/Melbourne"] = ("AEST", "AEDT"), ["Australia/Brisbane"] = ("AEST", "AEST"),
        ["Australia/Adelaide"] = ("ACST", "ACDT"), ["Australia/Perth"] = ("AWST", "AWST"), ["Pacific/Auckland"] = ("NZST", "NZDT"),
        ["Africa/Johannesburg"] = ("SAST", "SAST"), ["Africa/Cairo"] = ("EET", "EEST"), ["Africa/Lagos"] = ("WAT", "WAT"),
        ["Africa/Nairobi"] = ("EAT", "EAT")
    };

    private DateTime _hostTimezoneRequested = DateTime.MinValue;
    private bool _hostTimezoneFetching;
    private volatile string _hostTimezone = string.Empty;

    private void CreateTimezoneSettings()
    {
        CreateTextBox(MCBParitySetting.TimezoneOverride, "Timezone abbreviation override",
            "Text to show as the timezone variable instead of the detected abbreviation (e.g. CEST). Leave empty to detect.", string.Empty);
    }

    private void CreateTimezoneVariables()
    {
        CreateVariable<string>(MCBParityVariable.timezone, "Timezone Abbreviation (e.g. CEST)");
        CreateVariable<string>(MCBParityVariable.timezone_offset, "Timezone Offset (e.g. +02:00)");
    }

    private void StartTimezone()
    {
        _hostTimezoneRequested = DateTime.MinValue;
        _hostTimezone = string.Empty;
    }

    [ModuleUpdate(ModuleUpdateMode.Custom, true, 1000)]
    private void UpdateTimezone()
    {
        var now = DateTime.Now;
        var offset = TimeZoneInfo.Local.GetUtcOffset(now);
        SetVariableValue(MCBParityVariable.timezone_offset, FormatOffset(offset));

        var overrideText = (GetSettingValue<string>(MCBParitySetting.TimezoneOverride) ?? string.Empty).Trim();
        if (overrideText.Length > 0)
        {
            SetVariableValue(MCBParityVariable.timezone, overrideText);
            return;
        }

        if ((LinuxUtils.IsWineOnLinux || LinuxUtils.IsLinux) && !_hostTimezoneFetching && DateTime.UtcNow - _hostTimezoneRequested >= HostTimezoneRefresh)
        {
            _hostTimezoneRequested = DateTime.UtcNow;
            _hostTimezoneFetching = true;
            _ = Task.Run(FetchHostTimezone);
        }

        var host = _hostTimezone;
        SetVariableValue(MCBParityVariable.timezone, host.Length > 0 ? host : FallbackAbbreviation(now, offset));
    }

    /// <summary>Runs `date +%Z` on the Linux host. Blocking, so always called from a worker task.</summary>
    private void FetchHostTimezone()
    {
        try
        {
            string output;
            if (LinuxUtils.IsLinux)
            {
                output = LinuxUtils.RunShell("date +%Z");
            }
            else
            {
                // The Wine bridge cannot capture stdout; the host shell writes to its own $HOME, which
                // this process can read back through the Z: drive (same pattern as vrcosc_hwstats.sh).
                LinuxUtils.RunHost($"sh -c 'date +%Z > $HOME/{HostTimezoneFile}'", ex => Log($"Host timezone lookup failed: {ex.Message}"));
                var file = Path.Combine(LinuxUtils.GetWineHomeDir(), HostTimezoneFile);
                output = File.Exists(file) ? File.ReadAllText(file) : string.Empty;
            }

            output = output.Trim();
            if (output.Length is > 0 and <= 8 && output.All(c => char.IsLetterOrDigit(c) || c is '+' or '-'))
            {
                _hostTimezone = output;
            }
            else if (_hostTimezone.Length == 0)
            {
                Log("Host returned no usable timezone abbreviation; using the built-in fallback.");
            }
        }
        catch (IOException ex) { Log($"Host timezone lookup failed: {ex.Message}"); }
        catch (UnauthorizedAccessException ex) { Log($"Host timezone lookup failed: {ex.Message}"); }
        finally { _hostTimezoneFetching = false; }
    }

    private static string FormatOffset(TimeSpan offset)
        => (offset < TimeSpan.Zero ? "-" : "+") + offset.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

    private static string FallbackAbbreviation(DateTime now, TimeSpan offset)
    {
        var local = TimeZoneInfo.Local;
        var iana = local.Id;
        if (OperatingSystem.IsWindows() && TimeZoneInfo.TryConvertWindowsIdToIanaId(local.Id, out var converted)) iana = converted;

        if (IanaAbbreviations.TryGetValue(iana, out var names))
            return local.IsDaylightSavingTime(now) ? names.Daylight : names.Standard;

        if (offset == TimeSpan.Zero) return "UTC";

        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var magnitude = offset.Duration();
        return magnitude.Minutes == 0
            ? $"UTC{sign}{magnitude.Hours}"
            : $"UTC{sign}{magnitude.Hours}:{magnitude.Minutes:00}";
    }
}
