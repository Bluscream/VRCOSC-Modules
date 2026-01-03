// This is free and unencumbered software released into the public domain.
// For more information, please refer to <https://unlicense.org>

namespace Bluscream.Modules;

public enum DesktopFPSSetting
{
    UpdateInterval,
    SmoothingWindow,
    TrackVRChatFPS,
    TrackSystemFPS,
    LogFPS
}

public enum DesktopFPSParameter
{
    VRChatFPS,
    SystemFPS,
    AverageVRChatFPS,
    AverageSystemFPS,
    MinVRChatFPS,
    MaxVRChatFPS
}

public enum DesktopFPSVariable
{
    VRChatFPS,
    SystemFPS,
    AverageVRChatFPS,
    AverageSystemFPS,
    MinVRChatFPS,
    MaxVRChatFPS,
    VRChatProcessFound
}

public enum DesktopFPSState
{
    Monitoring,
    VRChatNotFound,
    Error
}

public enum DesktopFPSEvent
{
    OnVRChatFPSChanged,
    OnSystemFPSChanged,
    OnVRChatProcessFound,
    OnVRChatProcessLost
}
