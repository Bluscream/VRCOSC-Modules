// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Shared OpenXR session owned by the module suite. One instance, one headless session,
// one thread — the three modules read snapshots from it instead of each opening their own.

using System.Diagnostics;
using Silk.NET.OpenXR;

namespace VRCOSC.Modules.OpenXR;

/// <summary>
/// Owns the OpenXR instance and a headless (XR_MND_headless) session on a dedicated thread,
/// pumps events, runs the frame loop, syncs actions and hand trackers, and publishes an
/// immutable <see cref="OpenXRSnapshot"/> per frame. Reference counted: the first module to
/// <see cref="Acquire"/> starts the thread, the last to <see cref="Release"/> stops it.
/// </summary>
/// <remarks>
/// Why this exists: the modules used to each create an instance, and none of them ever got a
/// session into the running state — no event pump, no xrBeginSession, no action-set
/// attachment, no frame loop — so nothing downstream (FPS, haptics, hand joints) could work.
/// OpenXR is an application API: without a session in SYNCHRONIZED or better, every query
/// either fails or returns inactive data. The runtime is unavailable until a headset is
/// connected (WiVRn only registers itself as active runtime then), so initialisation is
/// retried on a timer instead of failing once at module start.
/// </remarks>
internal sealed unsafe partial class OpenXRRuntime
{
    public static OpenXRRuntime Shared { get; } = new();

    private const string AppName = "VRCOSC Bluscream Modules";
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan FallbackFramePeriod = TimeSpan.FromMilliseconds(1000d / 72d);

    private const string ExtHeadless = "XR_MND_headless";
    private const string ExtOverlay = "XR_EXTX_overlay";
    private const string ExtHandTrackingName = "XR_EXT_hand_tracking";
    private const string ExtHandTrackingDataSource = "XR_EXT_hand_tracking_data_source";
    private const string ExtDisplayRefreshRate = "XR_FB_display_refresh_rate";
    private const string ExtWin32Time = "XR_KHR_win32_convert_performance_counter_time";

    private readonly object _sync = new();
    private readonly List<Action<string>> _loggers = new();
    private readonly HashSet<string> _loggedOnce = new();
    private int _refCount;
    private Thread? _thread;
    private CancellationTokenSource? _cts;

    private XR? _xr;
    private Instance _instance;
    private ulong _systemId;
    private Session _session;
    private Space _localSpace;
    private Space _viewSpace;
    private SessionState _state = SessionState.Unknown;
    private bool _running;
    private bool _overlaySession;
    private bool _waitFrameWorks = true;
    private long _lastPredictedTime;
    private long _lastPredictedPeriod;
    private readonly Stopwatch _sinceLastFrame = new();
    private DateTime _nextRetry = DateTime.MinValue;
    private DateTime _nextRefreshRatePoll = DateTime.MinValue;
    private string _runtimeName = string.Empty;
    private string _systemName = string.Empty;
    private float _refreshRate;
    private bool _headTracked;
    private delegate* unmanaged[Cdecl]<Session, float*, Result> _getDisplayRefreshRate;
    private delegate* unmanaged[Cdecl]<Instance, long*, long*, Result> _convertWin32Time;

    private volatile OpenXRSnapshot _snapshot = OpenXRSnapshot.Empty;

    /// <summary>Latest published state. Never null; <see cref="OpenXRSnapshot.Empty"/> until initialised.</summary>
    public OpenXRSnapshot Snapshot => _snapshot;

    /// <summary>
    /// Request the session as an XR_EXTX_overlay session so it can receive input while another
    /// application (VRChat) is focused. Falls back to a plain session if the runtime refuses.
    /// Read at initialisation, so set it before the first <see cref="Acquire"/>.
    /// </summary>
    public bool UseOverlaySession { get; set; } = true;

    public void Acquire(Action<string> log)
    {
        lock (_sync)
        {
            _loggers.Add(log);
            _refCount++;
            if (_thread is not null) return;

            _cts = new CancellationTokenSource();
            _thread = new Thread(() => ThreadMain(_cts.Token)) { Name = "OpenXR", IsBackground = true };
            _thread.Start();
        }
    }

    public void Release(Action<string> log)
    {
        Thread? toJoin;
        lock (_sync)
        {
            _loggers.Remove(log);
            _refCount = Math.Max(0, _refCount - 1);
            if (_refCount > 0 || _thread is null) return;

            _cts?.Cancel();
            toJoin = _thread;
            _thread = null;
        }

        if (!toJoin.Join(TimeSpan.FromSeconds(5)))
            Log("OpenXR thread did not stop within 5 s; leaving it to finish in the background.");
    }

    private void Log(string message)
    {
        Action<string>? target;
        lock (_sync) target = _loggers.Count > 0 ? _loggers[0] : null;
        target?.Invoke(message);
    }

    /// <summary>Logs a message the first time it is seen; repeated per-frame failures would otherwise flood the log.</summary>
    private void LogOnce(string message)
    {
        bool first;
        lock (_sync) first = _loggedOnce.Add(message);
        if (first) Log(message);
    }

    // ─────────────────────────── Thread ───────────────────────────

    private void ThreadMain(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_instance.Handle == 0)
                {
                    if (DateTime.UtcNow < _nextRetry) { Thread.Sleep(200); continue; }
                    if (!TryInitialise())
                    {
                        TearDown();
                        _nextRetry = DateTime.UtcNow + RetryInterval;
                        Publish();
                        continue;
                    }
                }

                PumpEvents();
                if (_instance.Handle == 0) continue; // torn down by an event

                if (_running) FrameStep();
                else Thread.Sleep(50);

                Publish();
            }
            catch (Exception ex)
            {
                Log($"OpenXR thread error: {ex}");
                TearDown();
                _nextRetry = DateTime.UtcNow + RetryInterval;
            }
        }

        TearDown();
        Publish();
    }

    // ─────────────────────────── Events ───────────────────────────

    private void PumpEvents()
    {
        var buffer = new EventDataBuffer { Type = StructureType.EventDataBuffer };

        for (var guard = 0; guard < 64; guard++)
        {
            buffer.Type = StructureType.EventDataBuffer;
            buffer.Next = null;
            var r = _xr!.PollEvent(_instance, &buffer);
            if (r == Result.EventUnavailable) return;
            if (r != Result.Success) { LogOnce($"xrPollEvent failed: {r}"); return; }

            switch (buffer.Type)
            {
                case StructureType.EventDataSessionStateChanged:
                    OnSessionStateChanged(((EventDataSessionStateChanged*)&buffer)->State);
                    if (_instance.Handle == 0) return;
                    break;

                case StructureType.EventDataInteractionProfileChanged:
                    RefreshInteractionProfiles();
                    break;

                case StructureType.EventDataInstanceLossPending:
                    Log("OpenXR instance loss pending; tearing down and retrying later.");
                    TearDown();
                    _nextRetry = DateTime.UtcNow + RetryInterval;
                    return;
            }
        }
    }

    private void OnSessionStateChanged(SessionState state)
    {
        Log($"OpenXR session state: {_state} -> {state}");
        _state = state;

        switch (state)
        {
            case SessionState.Ready:
                {
                    var beginInfo = new SessionBeginInfo
                    {
                        Type = StructureType.SessionBeginInfo,
                        PrimaryViewConfigurationType = ViewConfigurationType.PrimaryStereo
                    };
                    var r = _xr!.BeginSession(_session, &beginInfo);
                    if (r == Result.Success) { _running = true; _sinceLastFrame.Restart(); }
                    else Log($"xrBeginSession failed: {r}");
                    break;
                }

            case SessionState.Stopping:
                if (_running) _xr!.EndSession(_session);
                _running = false;
                break;

            case SessionState.LossPending:
            case SessionState.Exiting:
                Log("OpenXR session is going away; tearing down and retrying later.");
                TearDown();
                _nextRetry = DateTime.UtcNow + RetryInterval;
                break;
        }
    }

    // ─────────────────────────── Frame loop ───────────────────────────

    private void FrameStep()
    {
        long time;

        if (_waitFrameWorks)
        {
            var waitInfo = new FrameWaitInfo { Type = StructureType.FrameWaitInfo };
            var frameState = new FrameState { Type = StructureType.FrameState };
            var r = _xr!.WaitFrame(_session, &waitInfo, &frameState);
            if (r == Result.Success)
            {
                _lastPredictedTime = frameState.PredictedDisplayTime;
                _lastPredictedPeriod = frameState.PredictedDisplayPeriod;
                _sinceLastFrame.Restart();

                var beginInfo = new FrameBeginInfo { Type = StructureType.FrameBeginInfo };
                _xr.BeginFrame(_session, &beginInfo);

                time = _lastPredictedTime;
                UpdateFrameData(time);

                var endInfo = new FrameEndInfo
                {
                    Type = StructureType.FrameEndInfo,
                    DisplayTime = _lastPredictedTime,
                    EnvironmentBlendMode = EnvironmentBlendMode.Opaque,
                    LayerCount = 0,
                    Layers = null
                };
                r = _xr.EndFrame(_session, &endInfo);
                if (r != Result.Success) LogOnce($"xrEndFrame returned {r}");

                if (_sinceLastFrame.Elapsed < TimeSpan.FromMilliseconds(1))
                    Thread.Sleep(FramePeriod()); // headless WaitFrame may not pace; do it ourselves
                return;
            }

            _waitFrameWorks = false;
            Log($"xrWaitFrame returned {r}; falling back to a self-paced loop with estimated times.");
        }

        time = EstimateNow();
        if (time == 0)
        {
            LogOnce("No XrTime source available (xrWaitFrame unsupported and no time-conversion extension); hand joints and poses will stay inactive.");
        }

        UpdateFrameData(time);
        Thread.Sleep(FramePeriod());
    }

    private TimeSpan FramePeriod()
    {
        if (_refreshRate > 1f) return TimeSpan.FromSeconds(1d / _refreshRate);
        if (_lastPredictedPeriod > 0) return TimeSpan.FromTicks(_lastPredictedPeriod / 100);
        return FallbackFramePeriod;
    }

    private long EstimateNow()
    {
        if (_convertWin32Time is not null)
        {
            long qpc = Stopwatch.GetTimestamp();
            long xrTime = 0;
            if (_convertWin32Time(_instance, &qpc, &xrTime) == Result.Success) return xrTime;
        }

        if (_lastPredictedTime != 0)
            return _lastPredictedTime + _sinceLastFrame.Elapsed.Ticks * 100L;

        return 0;
    }

    private void UpdateFrameData(long time)
    {
        SyncInput();
        LocateHands(time);
        _headTracked = time != 0 && LocateHead(time);
        PollRefreshRate();
        ApplyQueuedHaptics();
    }

    private bool LocateHead(long time)
    {
        var location = new SpaceLocation { Type = StructureType.SpaceLocation };
        var r = _xr!.LocateSpace(_viewSpace, _localSpace, time, &location);
        if (r != Result.Success) { LogOnce($"xrLocateSpace(VIEW) failed: {r}"); return false; }
        return OpenXRHelper.IsTracked(location.LocationFlags);
    }

    private void PollRefreshRate()
    {
        if (_getDisplayRefreshRate is null || DateTime.UtcNow < _nextRefreshRatePoll) return;
        _nextRefreshRatePoll = DateTime.UtcNow + TimeSpan.FromSeconds(1);

        float rate = 0f;
        if (_getDisplayRefreshRate(_session, &rate) == Result.Success && rate > 0f) _refreshRate = rate;
        else if (_lastPredictedPeriod > 0) _refreshRate = 1_000_000_000f / _lastPredictedPeriod;
    }

    // ─────────────────────────── Publish / teardown ───────────────────────────

    private void Publish()
    {
        var rate = _refreshRate;
        if (rate <= 0f && _lastPredictedPeriod > 0) rate = 1_000_000_000f / _lastPredictedPeriod;

        _snapshot = new OpenXRSnapshot(
            RuntimeAvailable: _instance.Handle != 0,
            SessionRunning: _running,
            SessionState: _state,
            RuntimeName: _runtimeName,
            SystemName: _systemName,
            DisplayRefreshRate: rate,
            HeadTracked: _headTracked,
            Left: _handInputs[(int)XrHand.Left],
            Right: _handInputs[(int)XrHand.Right],
            LeftProfile: _profiles[(int)XrHand.Left],
            RightProfile: _profiles[(int)XrHand.Right]);
    }

    private void TearDown()
    {
        if (_xr is null) return;

        DestroyHands();
        DestroyInput();

        if (_running && _session.Handle != 0) _xr.EndSession(_session);
        _running = false;

        if (_viewSpace.Handle != 0) { _xr.DestroySpace(_viewSpace); _viewSpace = default; }
        if (_localSpace.Handle != 0) { _xr.DestroySpace(_localSpace); _localSpace = default; }
        if (_session.Handle != 0) { _xr.DestroySession(_session); _session = default; }
        if (_instance.Handle != 0) { _xr.DestroyInstance(_instance); _instance = default; }

        _state = SessionState.Unknown;
        _systemId = 0;
        _headTracked = false;
        _refreshRate = 0f;
        _lastPredictedTime = 0;
        _lastPredictedPeriod = 0;
        _getDisplayRefreshRate = null;
        _convertWin32Time = null;
        _handInputs[0] = HandInput.Inactive;
        _handInputs[1] = HandInput.Inactive;
        _profiles[0] = string.Empty;
        _profiles[1] = string.Empty;
    }
}
