# Bluscream Utilities

Shared utility package for all Bluscream VRCOSC modules. This is **not** a module itself - it's a collection of reusable utility functions.

## Purpose

Consolidates common functionality to avoid code duplication across modules:

- VRChatSettings
- Notifications
- HTTPServer
- Debug
- VRCXBridge

## Utilities Included

### Extensions (Static Extension Methods)

Comprehensive set of extension methods for common operations.

**String Extensions:**

- `IsNullOrWhiteSpace()`, `IsNullOrEmpty()`, `OrDefault(defaultValue)`
- `Truncate(maxLength, suffix)` - Smart string truncation
- `ToIntOrDefault()`, `ToFloatOrDefault()`, `ToBoolOrDefault()`
- `RemovePrefix(prefix)`, `RemoveSuffix(suffix)`

**Collection Extensions:**

- `ForEach(action)` - Functional foreach
- `IsNullOrEmpty()` - Null-safe collection check
- `GetValueOrDefault(key, default)` - Safe dictionary access
- `AddOrUpdate(key, value)` - Upsert for dictionaries
- `Chunk(size)` - Split into batches

**Task Extensions:**

- `FireAndForget(onException)` - Async without await
- `WithTimeout(ms, defaultValue)` - Timeout wrapper
- `RetryAsync(maxRetries, delayMs)` - Exponential backoff retry

**JSON Extensions:**

- `ToJson(indented)` - Serialize to JSON
- `FromJson<T>()` - Deserialize from JSON
- `TryGetJsonProperty<T>(name, out value)` - Safe property access

**Enum Extensions:**

- `GetDescription()` - Get [Description] attribute value
- `ToEnumOrDefault<T>(default)` - Safe enum parsing
- `GetValues<T>()` - Get all enum values

**Numeric Extensions:**

- `Clamp(min, max)` - Constrain value to range
- `IsBetween(min, max)` - Range check
- `Map(fromMin, fromMax, toMin, toMax)` - Remap value to new range
- `RoundTo(decimals)` - Round to decimal places

**DateTime Extensions:**

- `ToUnixTimestamp()`, `ToUnixTimestampMs()` - Unix time
- `FromUnixTimestamp()`, `FromUnixTimestampMs()` - From Unix time
- `ToIso8601()` - ISO 8601 format
- `ToTimeAgo()` - Human-readable relative time ("5m ago")

**Reflection Extensions:**

- `GetPropertyValue<T>(name, flags)` - Safe property get
- `SetPropertyValue(name, value, flags)` - Safe property set
- `GetFieldValue<T>(name, flags)` - Safe field get
- `InvokeMethod<T>(name, params, flags)` - Safe method invoke
- `HasAttribute<T>()`, `GetAttribute<T>()` - Attribute helpers

**Exception Extensions:**

- `GetFullMessage()` - Full message including inner exceptions
- `GetInnerExceptions()` - Enumerate all inner exceptions

**Type Conversion:**

- `ConvertTo<T>(defaultValue)` - Safe type conversion
- `As<T>()` - Safe cast
- `Is<T>()` - Type check

**Validation:**

- `ThrowIfNull(paramName)` - Null check with exception
- `ThrowIfNullOrEmpty(paramName)` - String null/empty check
- `IfNotNull(action)` - Conditional execution
- `IfNotNull(func)` - Conditional transformation

**Functional:**

- `Pipe(func)` - Functional piping
- `Tap(action)` - Execute and return (for chaining)
- `Match(some, none)` - Pattern matching for nullables

**OSC:**

- `NormalizeOscAddress()` - Ensure starts with /
- `IsValidOscAddress()` - Validate OSC address format
- `GetOscParameterName()` - Extract parameter name

**VRChat:**

- `IsVRChatAvatarId()` - Check if avtr_xxx format
- `IsVRChatUserId()` - Check if usr_xxx format
- `IsVRChatWorldId()` - Check if wrld_xxx format

**HTTP:**

- `AddQueryParameter(key, value)` - Add query param to URL
- `IsValidUrl()` - Validate HTTP/HTTPS URL

**Misc:**

- `MeasureTime(action)`, `MeasureTimeAsync(action)` - Performance timing
- `ToFileSize()` - Format bytes to "1.5 MB"

**VRCOSC Module Extensions:**

- `LoadSettings()` - Force reload settings from disk (alias for `ReflectionUtils.LoadFromDisk()`)
- `GetSettingsFilePath()` - Get path to module's JSON settings file
- `GetSettings()` - Read settings from disk as dictionary (works before module starts)
- `GetSetting<T>(name, defaultValue)` - Get specific setting value from disk
- `IsEnabled()` - Check if module is enabled in settings

### ReflectionUtils

Reflection-based utilities for accessing VRCOSC's internal APIs. All reflection operations are **cached** for optimal performance when called multiple times.

**AppManager Access:**

- `GetAppManager()` - Get AppManager singleton
- `GetModuleManager()` - Get ModuleManager instance
- `GetCurrentProfileId()` - Get active profile GUID
- `GetCurrentProfileModulesPath()` - Get path to current profile's modules directory

**ChatBox Operations:**

- `GetChatBoxManager()` - Get ChatBoxManager singleton
- `SendChatBox(text, minimalBackground)` - Send chatbox message
- `GetChatBoxStates(prefixFilter)` - Get all states, optionally filtered
- `GetChatBoxEvents(prefixFilter)` - Get all events, optionally filtered
- `GetVRCXStates()` - Get VRCX-specific states
- `GetVRCXEvents()` - Get VRCX-specific events

**OSC Operations:**

- `SendRawOSC(address, ...args)` - Send raw OSC message

**Module Control:**

- `StopModules()` - Stop all modules
- `StartModules()` - Start all modules
- `GetModules()` - Get all loaded modules

**Persistence:**

- `FlushToDisk()` - Save all module data
- `LoadFromDisk()` - Reload all module data

**Module ID Helpers:**

- `GetModuleId(module)` - Get module ID as VRCOSC sees it (e.g., "notificationsmodule")
- `GetModulePackageId(module)` - Get package ID ("local" for local modules)
- `GetModuleFullId(module)` - Get full ID for file naming (e.g., "local.notificationsmodule")

### VRCUtils

VRChat-specific utility functions.

**User ID:**

- `IsValidUserId(userId)` - Validate VRChat user ID format
- `ExpandKeyTemplate(template, userId)` - Expand {userId} templates
- `IsUserTemplate(key)` - Check if key contains {userId}

**Hashing:**

- `AddHashToKeyName(key)` - Generate VRChat-style hash
- `RemoveHashFromKeyName(hashedKey)` - Extract original key

## Usage Examples

```csharp
using Bluscream;

// === Reflection Utilities ===
ReflectionUtils.SendChatBox("Hello VRChat!", minimalBackground: true);
ReflectionUtils.SendRawOSC("/avatar/parameters/VRCEmote", 1);
ReflectionUtils.StopModules();
ReflectionUtils.FlushToDisk();

// === VRChat Utilities ===
if (VRCUtils.IsValidUserId("usr_xxx...")) { }
var key = VRCUtils.ExpandKeyTemplate("{userId}_setting", myUserId);
var hashed = VRCUtils.AddHashToKeyName("myKey"); // "myKey_h12345"

// === Extension Methods ===

// String extensions
var port = portString.ToIntOrDefault(8080);
var trimmed = title.Truncate(50, "...");
var safe = input.OrDefault("default");

// Collection extensions
myDict.AddOrUpdate("key", value);
var val = myDict.GetValueOrDefault("key", defaultVal);
items.ForEach(item => Console.WriteLine(item));

// JSON extensions
var json = myObject.ToJson(indented: true);
var obj = jsonString.FromJson<MyType>();

// Validation extensions
var notNull = value.ThrowIfNull("paramName");
url.IfNotNull(u => Console.WriteLine(u));

// OSC extensions
var addr = "avatar/parameters/Test".NormalizeOscAddress(); // "/avatar/parameters/Test"
var param = "/avatar/parameters/VRCEmote".GetOscParameterName(); // "VRCEmote"

// VRChat extensions
if (id.IsVRChatUserId()) { }
if (avatarId.IsVRChatAvatarId()) { }

// DateTime extensions
var ago = lastUpdate.ToTimeAgo(); // "5m ago"
var unix = DateTime.Now.ToUnixTimestamp();

// Numeric extensions
var clamped = value.Clamp(0, 100);
var mapped = value.Map(0, 255, 0.0, 1.0); // Map 0-255 to 0.0-1.0

// HTTP extensions
var url = "http://api.com".AddQueryParameter("key", "value");

// Functional extensions
var result = value.Pipe(x => x * 2).Tap(x => Log(x));

// Task extensions
await myTask.WithTimeout(5000, defaultValue);
task.FireAndForget(ex => Log(ex.Message));
```

## Namespace

Uses `Bluscream` namespace (not `Bluscream.Utils` or `Bluscream.Modules.Utilities`):

- ✅ Clean imports: `using Bluscream;`
- ✅ Flexible usage: `ReflectionUtils.SendChatBox()` or `Bluscream.ReflectionUtils.SendChatBox()`
- ✅ Better semantics: Utilities are not modules

## Why Not a Module?

This is a **utility package**, not a VRCOSC module:

- No `Module` base class
- No module attributes (`[ModuleTitle]`, etc.)
- Just static utility classes
- Won't appear in VRCOSC's module list
- Available to all your modules via `using Bluscream;`

## Migration Notes

**Functions moved from:**

- `VRCXBridge/Utils.cs` → `ReflectionUtils`
- `VRCXBridge/VRCOSCUtils.cs` → `ReflectionUtils` (duplicate removed)
- `VRChatSettings/VRChatUserIdHelper.cs` → `VRCUtils`
- `Debug/Classes/Reflection.cs` → Removed (was unused/disabled)

**Class renames:**

- `VRCOSCReflectionUtils` → `ReflectionUtils`
- `VRChatUtils` → `VRCUtils`

**After migration, modules should:**

1. Add `using Bluscream;`
2. Update references from `Utils.SendChatBox()` to `ReflectionUtils.SendChatBox()`
3. Update references from `VRChatUserIdHelper.IsValidUserId()` to `VRCUtils.IsValidUserId()`

---

_Created: November 3, 2025_
_Consolidated from multiple modules to reduce code duplication_
