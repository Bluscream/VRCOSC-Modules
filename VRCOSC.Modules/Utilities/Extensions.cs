// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace Bluscream;

/// <summary>
/// Extension methods - all implementations are in corresponding Utils classes
/// These are thin wrappers that make utilities usable as extension methods
/// </summary>
public static class Extensions
{
    #region String Extensions => StringUtils

    public static bool IsNullOrWhiteSpace(this string? str) => StringUtils.IsNullOrWhiteSpace(str);
    public static bool IsNullOrEmpty(this string? str) => StringUtils.IsNullOrEmpty(str);
    public static string OrDefault(this string? str, string defaultValue) => StringUtils.OrDefault(str, defaultValue);
    public static string Truncate(this string str, int maxLength, string suffix = "...") => StringUtils.Truncate(str, maxLength, suffix);
    public static int ToIntOrDefault(this string? str, int defaultValue = 0) => StringUtils.ToIntOrDefault(str, defaultValue);
    public static float ToFloatOrDefault(this string? str, float defaultValue = 0f) => StringUtils.ToFloatOrDefault(str, defaultValue);
    public static bool ToBoolOrDefault(this string? str, bool defaultValue = false) => StringUtils.ToBoolOrDefault(str, defaultValue);
    public static string RemovePrefix(this string str, string prefix) => StringUtils.RemovePrefix(str, prefix);
    public static string RemoveSuffix(this string str, string suffix) => StringUtils.RemoveSuffix(str, suffix);

    #endregion

    #region Collection Extensions => CollectionUtils

    public static void ForEach<T>(this IEnumerable<T> source, Action<T> action) => CollectionUtils.ForEach(source, action);
    public static bool IsNullOrEmpty<T>(this IEnumerable<T>? source) => CollectionUtils.IsNullOrEmpty(source);
    public static TValue? GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue? defaultValue = default) where TKey : notnull => CollectionUtils.GetValueOrDefault(dict, key, defaultValue);
    public static void AddOrUpdate<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue value) where TKey : notnull => CollectionUtils.AddOrUpdate(dict, key, value);
    public static IEnumerable<IEnumerable<T>> Chunk<T>(this IEnumerable<T> source, int chunkSize) => CollectionUtils.Chunk(source, chunkSize);

    #endregion

    #region Task Extensions => TaskUtils

    public static void FireAndForget(this Task task, Action<Exception>? onException = null) => TaskUtils.FireAndForget(task, onException);
    public static Task<T> WithTimeout<T>(this Task<T> task, int timeoutMs, T defaultValue = default!) => TaskUtils.WithTimeout(task, timeoutMs, defaultValue);
    public static Task<bool> WithTimeout(this Task task, int timeoutMs) => TaskUtils.WithTimeout(task, timeoutMs);
    public static Task<T?> RetryAsync<T>(Func<Task<T>> action, int maxRetries = 3, int initialDelayMs = 100) => TaskUtils.RetryAsync(action, maxRetries, initialDelayMs);
    public static Task RetryAsync(Func<Task> action, int maxRetries = 3, int initialDelayMs = 100) => TaskUtils.RetryAsync(action, maxRetries, initialDelayMs);

    #endregion

    #region JSON Extensions => JsonUtils

    public static string ToJson(this object obj, bool indented = false) => JsonUtils.ToJson(obj, indented);
    public static T? FromJson<T>(this string json) => JsonUtils.FromJson<T>(json);
    public static bool TryGetJsonProperty<T>(this JsonElement element, string propertyName, out T? value) => JsonUtils.TryGetJsonProperty(element, propertyName, out value);

    #endregion

    #region Enum Extensions => EnumUtils

    public static string GetDescription(this Enum value) => EnumUtils.GetDescription(value);
    public static T ToEnumOrDefault<T>(this string? str, T defaultValue = default!) where T : struct, Enum => EnumUtils.ToEnumOrDefault(str, defaultValue);
    public static IEnumerable<T> GetValues<T>() where T : Enum => EnumUtils.GetValues<T>();

    #endregion

    #region Numeric Extensions => NumericUtils

    public static T Clamp<T>(this T value, T min, T max) where T : IComparable<T> => NumericUtils.Clamp(value, min, max);
    public static bool IsBetween<T>(this T value, T min, T max) where T : IComparable<T> => NumericUtils.IsBetween(value, min, max);
    public static double Map(this double value, double fromMin, double fromMax, double toMin, double toMax) => NumericUtils.Map(value, fromMin, fromMax, toMin, toMax);
    public static double RoundTo(this double value, int decimals) => NumericUtils.RoundTo(value, decimals);

    #endregion

    #region DateTime Extensions => DateTimeUtils

    public static long ToUnixTimestamp(this DateTime dateTime) => DateTimeUtils.ToUnixTimestamp(dateTime);
    public static long ToUnixTimestampMs(this DateTime dateTime) => DateTimeUtils.ToUnixTimestampMs(dateTime);
    public static DateTime FromUnixTimestamp(this long timestamp) => DateTimeUtils.FromUnixTimestamp(timestamp);
    public static DateTime FromUnixTimestampMs(this long timestamp) => DateTimeUtils.FromUnixTimestampMs(timestamp);
    public static string ToIso8601(this DateTime dateTime) => DateTimeUtils.ToIso8601(dateTime);
    public static string ToTimeAgo(this DateTime dateTime) => DateTimeUtils.ToTimeAgo(dateTime);

    #endregion

    #region Reflection Extensions => TypeUtils

    public static T? GetPropertyValue<T>(this object obj, string propertyName, BindingFlags flags = BindingFlags.Public | BindingFlags.Instance) => TypeUtils.GetPropertyValue<T>(obj, propertyName, flags);
    public static bool SetPropertyValue(this object obj, string propertyName, object? value, BindingFlags flags = BindingFlags.Public | BindingFlags.Instance) => TypeUtils.SetPropertyValue(obj, propertyName, value, flags);
    public static T? GetFieldValue<T>(this object obj, string fieldName, BindingFlags flags = BindingFlags.Public | BindingFlags.Instance) => TypeUtils.GetFieldValue<T>(obj, fieldName, flags);
    public static T? InvokeMethod<T>(this object obj, string methodName, object?[]? parameters = null, BindingFlags flags = BindingFlags.Public | BindingFlags.Instance) => TypeUtils.InvokeMethod<T>(obj, methodName, parameters, flags);
    public static bool HasAttribute<T>(this Type type) where T : Attribute => TypeUtils.HasAttribute<T>(type);
    public static T? GetAttribute<T>(this Type type) where T : Attribute => TypeUtils.GetAttribute<T>(type);

    #endregion

    #region Exception Extensions => ExceptionUtils

    public static string GetFullMessage(this Exception ex) => ExceptionUtils.GetFullMessage(ex);
    public static IEnumerable<Exception> GetInnerExceptions(this Exception ex) => ExceptionUtils.GetInnerExceptions(ex);

    #endregion

    #region Type Conversion Extensions => TypeUtils

    public static T? ConvertTo<T>(this object? value, T? defaultValue = default) => TypeUtils.ConvertTo(value, defaultValue);
    public static T? As<T>(this object? obj) where T : class => TypeUtils.As<T>(obj);
    public static bool Is<T>(this object? obj) => TypeUtils.Is<T>(obj);

    #endregion

    #region Validation Extensions => ValidationUtils

    public static T ThrowIfNull<T>(this T? value, string? paramName = null) where T : class => ValidationUtils.ThrowIfNull(value, paramName);
    public static string ThrowIfNullOrEmpty(this string? value, string? paramName = null) => ValidationUtils.ThrowIfNullOrEmpty(value, paramName);
    public static T? IfNotNull<T>(this T? value, Action<T> action) where T : class => ValidationUtils.IfNotNull(value, action);
    public static TResult? IfNotNull<T, TResult>(this T? value, Func<T, TResult> func) where T : class => ValidationUtils.IfNotNull(value, func);

    #endregion

    #region Functional Extensions => FunctionalUtils

    public static TResult Pipe<T, TResult>(this T value, Func<T, TResult> func) => FunctionalUtils.Pipe(value, func);
    public static T Tap<T>(this T value, Action<T> action) => FunctionalUtils.Tap(value, action);
    public static TResult Match<T, TResult>(this T? value, Func<T, TResult> some, Func<TResult> none) where T : class => FunctionalUtils.Match(value, some, none);

    #endregion

    #region OSC Extensions => OscUtils

    public static string NormalizeOscAddress(this string address) => OscUtils.NormalizeOscAddress(address);
    public static bool IsValidOscAddress(this string address) => OscUtils.IsValidOscAddress(address);
    public static string GetOscParameterName(this string address) => OscUtils.GetOscParameterName(address);

    #endregion

    #region VRChat Extensions => VRCUtils

    public static bool IsVRChatAvatarId(this string? str) => VRCUtils.IsVRChatAvatarId(str);
    public static bool IsVRChatUserId(this string? str) => VRCUtils.IsValidUserId(str);
    public static bool IsVRChatWorldId(this string? str) => VRCUtils.IsVRChatWorldId(str);

    #endregion

    #region HTTP Extensions => HttpUtils

    public static string AddQueryParameter(this string url, string key, string value) => HttpUtils.AddQueryParameter(url, key, value);
    public static bool IsValidUrl(this string? str) => HttpUtils.IsValidUrl(str);

    #endregion

    #region File/Misc Extensions => FileUtils

    public static string ToFileSize(this long bytes) => FileUtils.ToFileSize(bytes);
    public static TimeSpan MeasureTime(Action action) => FileUtils.MeasureTime(action);
    public static Task<TimeSpan> MeasureTimeAsync(Func<Task> action) => FileUtils.MeasureTimeAsync(action);

    #endregion

    #region VRCOSC Module Extensions => ReflectionUtils & ModuleUtils

    public static bool LoadSettings(this VRCOSC.App.SDK.Modules.Module module) => ReflectionUtils.LoadFromDisk();
    public static string? GetSettingsFilePath(this VRCOSC.App.SDK.Modules.Module module) => ReflectionUtils.GetModuleSettingsFilePath(module);
    public static Dictionary<string, System.Text.Json.JsonElement>? GetSettings(this VRCOSC.App.SDK.Modules.Module module) => ReflectionUtils.GetModuleSettings(module);
    public static T? GetSetting<T>(this VRCOSC.App.SDK.Modules.Module module, string settingName, T? defaultValue = default) => ReflectionUtils.GetModuleSetting(module, settingName, defaultValue);
    public static bool IsEnabled(this VRCOSC.App.SDK.Modules.Module module) => ReflectionUtils.IsModuleEnabled(module);
    public static bool SendParameterSafe(this VRCOSC.App.SDK.Modules.Module module, Enum lookup, object value) => ModuleUtils.SendParameterSafe(module, lookup, value);
    public static bool SendParameterSafe(this VRCOSC.App.SDK.Modules.Module module, string name, object value) => ModuleUtils.SendParameterSafe(module, name, value);

    #endregion
}
