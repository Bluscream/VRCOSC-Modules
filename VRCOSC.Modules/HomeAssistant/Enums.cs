// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

namespace Bluscream.Modules;

public enum HomeAssistantSetting
{
    ServerUrl,
    AccessToken,
    OscPrefix,
    EnableWebSocket,
    LogDebug,
    LogOscParams,
    EntityFilter,
    TemplateVariables,
    RegisterAllEntityVariables
}

public enum HomeAssistantParameter
{
    Connected,
    EventReceived,
    Failed
}

public enum HomeAssistantVariable
{
    Connected,
    LastEntity,
    LastState,
    StatesCount
}

public enum HomeAssistantState
{
    Disconnected,
    Connecting,
    Connected,
    Error
}

public enum HomeAssistantEvent
{
    OnStateChanged,
    OnServiceExecuted,
    OnError
}
