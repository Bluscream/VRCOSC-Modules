// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Session statistics over a stream of heart rate samples: min/max since reset, a rolling
// average and a trend arrow derived from a rolling window. Pure logic, no SDK dependency.

namespace Bluscream.Modules.HeartrateStats;

internal sealed class HeartrateSession
{
    public const string TrendUp = "↑";
    public const string TrendFlat = "→";
    public const string TrendDown = "↓";

    private readonly record struct Sample(DateTimeOffset At, int Bpm);

    private readonly object _lock = new();
    private readonly List<Sample> _samples = new();

    public int Current { get; private set; }
    public int Min { get; private set; }
    public int Max { get; private set; }
    public DateTimeOffset LastSampleAt { get; private set; } = DateTimeOffset.MinValue;

    /// <summary>Oldest sample kept; must cover the largest of the trend window and the average period.</summary>
    public TimeSpan Retention { get; set; } = TimeSpan.FromMinutes(2);

    public void Reset()
    {
        lock (_lock)
        {
            _samples.Clear();
            Current = 0;
            Min = 0;
            Max = 0;
            LastSampleAt = DateTimeOffset.MinValue;
        }
    }

    /// <summary>Resets only the session extremes, keeping the rolling window intact.</summary>
    public void ResetExtremes()
    {
        lock (_lock)
        {
            Min = Current;
            Max = Current;
        }
    }

    public void Add(int bpm, DateTimeOffset now)
    {
        if (bpm <= 0) return;

        lock (_lock)
        {
            Current = bpm;
            LastSampleAt = now;
            Min = Min == 0 ? bpm : Math.Min(Min, bpm);
            Max = Math.Max(Max, bpm);

            _samples.Add(new Sample(now, bpm));
            var cutoff = now - Retention;
            _samples.RemoveAll(s => s.At < cutoff);
        }
    }

    public bool IsReceiving(TimeSpan timeout, DateTimeOffset now) => LastSampleAt != DateTimeOffset.MinValue && LastSampleAt + timeout >= now;

    public int Average(TimeSpan period, DateTimeOffset now)
    {
        lock (_lock)
        {
            var cutoff = now - period;
            var sum = 0;
            var count = 0;

            foreach (var s in _samples)
            {
                if (s.At < cutoff) continue;
                sum += s.Bpm;
                count++;
            }

            return count == 0 ? 0 : (int)MathF.Round(sum / (float)count);
        }
    }

    /// <summary>
    /// Compares the mean of the newer half of the window against the older half. A difference
    /// of at least <paramref name="threshold"/> bpm yields an arrow, otherwise flat.
    /// </summary>
    public string Trend(TimeSpan window, int threshold, DateTimeOffset now)
    {
        lock (_lock)
        {
            var cutoff = now - window;
            var inWindow = _samples.Where(s => s.At >= cutoff).ToList();
            if (inWindow.Count < 2) return TrendFlat;

            var half = inWindow.Count / 2;
            var older = inWindow.Take(half).Average(s => s.Bpm);
            var newer = inWindow.Skip(half).Average(s => s.Bpm);
            var delta = newer - older;

            if (delta >= threshold) return TrendUp;
            if (delta <= -threshold) return TrendDown;
            return TrendFlat;
        }
    }
}
