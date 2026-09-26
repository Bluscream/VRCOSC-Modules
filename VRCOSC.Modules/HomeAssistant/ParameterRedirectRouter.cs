// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Runtime side of parameter redirects: matches received parameters and entity state changes to
// rows, rate-limits numeric updates per row, and reports a bad row only once.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Bluscream.Modules.HomeAssistant;

public sealed class ParameterRedirectRouter
{
    private const string AvatarParametersPrefix = "/avatar/parameters/";

    private readonly Func<List<ParameterRedirect>> _rows;
    private readonly Func<int> _rateLimitMs;
    private readonly Func<string, IReadOnlyDictionary<string, object?>?> _attributesOf;
    private readonly Func<HaServiceCall, string, Task<bool>> _callService;
    private readonly Action<string, object> _sendParameter;
    private readonly Action<string> _log;
    private readonly Action<string> _logDebug;

    private readonly ConcurrentDictionary<string, byte> _rejected = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<ParameterRedirect, PendingSend> _pending = new();

    private sealed class PendingSend
    {
        public object? Value;
        public int Scheduled;
    }

    public ParameterRedirectRouter(
        Func<List<ParameterRedirect>> rows,
        Func<int> rateLimitMs,
        Func<string, IReadOnlyDictionary<string, object?>?> attributesOf,
        Func<HaServiceCall, string, Task<bool>> callService,
        Action<string, object> sendParameter,
        Action<string> log,
        Action<string> logDebug)
    {
        _rows = rows;
        _rateLimitMs = rateLimitMs;
        _attributesOf = attributesOf;
        _callService = callService;
        _sendParameter = sendParameter;
        _log = log;
        _logDebug = logDebug;
    }

    /// <summary>Forget which rows were reported as bad, so edited rows get a fresh verdict on the next start.</summary>
    public void Reset()
    {
        _rejected.Clear();
        _pending.Clear();
    }

    /// <summary>
    /// Applies every enabled OSC-to-HA row whose source matches the received parameter.
    /// Returns true when at least one row matched, so the caller can skip its prefix-based handling.
    /// </summary>
    public bool HandleParameter(string parameterName, object value)
    {
        var name = StripAvatarPrefix(parameterName);
        var matched = false;

        foreach (var row in _rows().Where(r => r.Enabled.Value && ParameterRedirectMapping.IsOscToHa(r.Conversion.Value) && SourceMatches(r, name)))
        {
            matched = true;
            var domain = ValidTarget(row);
            if (domain is null) continue;

            if (value is bool || _rateLimitMs() <= 0)
            {
                _ = Dispatch(row, domain, value);
            }
            else
            {
                Schedule(row, domain, value);
            }
        }

        return matched;
    }

    /// <summary>Applies every enabled HA-to-OSC row targeting the changed entity.</summary>
    public void HandleStateChanged(string entityId, string state, IReadOnlyDictionary<string, object?>? attributes)
    {
        foreach (var row in _rows().Where(r => r.Enabled.Value && !ParameterRedirectMapping.IsOscToHa(r.Conversion.Value) && string.Equals(r.Target.Value.Trim(), entityId, StringComparison.OrdinalIgnoreCase)))
        {
            var domain = ValidTarget(row);
            if (domain is null) continue;

            var source = StripAvatarPrefix(row.Source.Value);
            if (source.Length == 0)
            {
                RejectOnce(row, "source parameter is empty");
                continue;
            }

            var converted = ParameterRedirectMapping.ToParameterValue(row, domain, state, attributes);
            if (converted is null)
            {
                _logDebug($"Redirect {entityId} -> {source}: state '{state}' has no {row.Conversion.Value} representation");
                continue;
            }

            _sendParameter(source, converted);
            _logDebug($"Redirect {entityId} -> {source} = {converted}");
        }
    }

    private void Schedule(ParameterRedirect row, string domain, object value)
    {
        var pending = _pending.GetOrAdd(row, _ => new PendingSend());
        pending.Value = value;

        if (Interlocked.Exchange(ref pending.Scheduled, 1) != 0) return;

        _ = Task.Run(async () =>
        {
            await Task.Delay(_rateLimitMs());
            Interlocked.Exchange(ref pending.Scheduled, 0);
            var latest = pending.Value;
            if (latest is not null) await Dispatch(row, domain, latest);
        });
    }

    private async Task Dispatch(ParameterRedirect row, string domain, object value)
    {
        var entityId = row.Target.Value.Trim();
        var attributes = _attributesOf(entityId);
        var call = ParameterRedirectMapping.ToServiceCall(row, domain, value, attributes);

        if (call is null)
        {
            if (row.Conversion.Value == RedirectConversion.IntToValue && domain is ("select" or "input_select"))
            {
                RejectOnce(row, "no options known for the select entity yet");
            }
            else
            {
                _logDebug($"Redirect {row.Source.Value} -> {entityId}: value {value} produces no service call");
            }

            return;
        }

        _logDebug($"Redirect {row.Source.Value} = {value} -> {call.Domain}.{call.Service} {entityId}");
        await _callService(call, entityId);
    }

    /// <summary>The target's domain when the row is usable, otherwise null after logging the reason once.</summary>
    private string? ValidTarget(ParameterRedirect row)
    {
        var entityId = row.Target.Value.Trim();
        var domain = ParameterRedirectMapping.DomainOf(entityId);

        if (domain is null)
        {
            RejectOnce(row, $"'{entityId}' is not a valid entity id (expected domain.object_id)");
            return null;
        }

        if (!ParameterRedirectMapping.KnownDomains.Contains(domain))
        {
            RejectOnce(row, $"domain '{domain}' has no redirect mapping");
            return null;
        }

        return domain;
    }

    private void RejectOnce(ParameterRedirect row, string reason)
    {
        var key = $"{row.Source.Value}|{row.Target.Value}|{row.Conversion.Value}|{reason}";
        if (_rejected.TryAdd(key, 0)) _log($"Parameter redirect '{row.Source.Value}' -> '{row.Target.Value}' skipped: {reason}.");
    }

    /// <summary>Matches the row's source exactly, or as a suffix so generator prefixes (VRCFury's VF52_.../) still match.</summary>
    private static bool SourceMatches(ParameterRedirect row, string parameterName)
    {
        var source = StripAvatarPrefix(row.Source.Value);
        if (source.Length == 0) return false;

        return parameterName.Equals(source, StringComparison.OrdinalIgnoreCase)
               || parameterName.EndsWith("/" + source, StringComparison.OrdinalIgnoreCase);
    }

    private static string StripAvatarPrefix(string name)
    {
        var trimmed = name.Trim();
        return trimmed.StartsWith(AvatarParametersPrefix, StringComparison.OrdinalIgnoreCase) ? trimmed[AvatarParametersPrefix.Length..] : trimmed.TrimStart('/');
    }
}
