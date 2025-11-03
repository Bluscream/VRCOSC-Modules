// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;

namespace Bluscream;

/// <summary>
/// Numeric utility functions
/// </summary>
public static class NumericUtils
{
    public static T Clamp<T>(T value, T min, T max) where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0) return min;
        if (value.CompareTo(max) > 0) return max;
        return value;
    }
    
    public static bool IsBetween<T>(T value, T min, T max) where T : IComparable<T>
        => value.CompareTo(min) >= 0 && value.CompareTo(max) <= 0;
    
    public static double Map(double value, double fromMin, double fromMax, double toMin, double toMax)
        => (value - fromMin) / (fromMax - fromMin) * (toMax - toMin) + toMin;
    
    public static double RoundTo(double value, int decimals)
        => Math.Round(value, decimals);
}
