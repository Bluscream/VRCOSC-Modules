// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Heart rate sources. The WebSocket base runs its own connect/receive loop instead of the
// SDK's WebSocketClient: that client reuses a single ClientWebSocket for every reconnect
// attempt (a ClientWebSocket cannot reconnect once a connect has failed or aborted) and
// gives up after a fixed number of attempts, so a dropped connection stays dropped until
// the module is restarted. This loop uses a fresh socket per attempt and exponential backoff.

using System.Net.WebSockets;
using System.Text;

namespace Bluscream.Modules.HeartrateStats;

internal interface IHeartrateSource
{
    bool IsConnected { get; }
    event Action<int>? HeartrateReceived;
    event Action<bool>? ConnectionChanged;
    void Start();
    Task StopAsync();
}

internal abstract class WebSocketHeartrateSource : IHeartrateSource
{
    private static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);

    private readonly Action<string> _log;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private ClientWebSocket? _socket;
    private bool _connected;

    protected WebSocketHeartrateSource(Action<string> log)
    {
        _log = log;
    }

    public bool IsConnected => _connected;

    public event Action<int>? HeartrateReceived;
    public event Action<bool>? ConnectionChanged;

    protected abstract string Name { get; }
    protected abstract Uri Uri { get; }

    /// <summary>Interval for <see cref="OnKeepAliveAsync"/>; null disables the keep-alive task.</summary>
    protected virtual TimeSpan? KeepAliveInterval => null;

    protected virtual Task OnConnectedAsync(CancellationToken ct) => Task.CompletedTask;
    protected virtual Task OnKeepAliveAsync(CancellationToken ct) => Task.CompletedTask;

    /// <summary>Parses one text frame; return the bpm or null when the frame carries no reading.</summary>
    protected abstract int? ParseHeartrate(string message);

    protected void Log(string message) => _log($"[{Name}] {message}");

    public void Start()
    {
        if (_loop is not null) return;

        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        if (_cts is null || _loop is null) return;

        await _cts.CancelAsync().ConfigureAwait(false);

        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // expected on stop
        }

        _cts.Dispose();
        _cts = null;
        _loop = null;
    }

    protected async Task SendAsync(string payload, CancellationToken ct)
    {
        var socket = _socket;
        if (socket is null || socket.State != WebSocketState.Open) return;

        await _sendLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            await socket.SendAsync(Encoding.UTF8.GetBytes(payload), WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    // Every exception is caught here: this runs on a thread-pool thread and an unhandled
    // exception there terminates the VRCOSC process.
    private async Task RunAsync(CancellationToken ct)
    {
        var backoff = InitialBackoff;

        while (!ct.IsCancellationRequested)
        {
            var connectedAt = DateTimeOffset.MinValue;

            try
            {
                using var socket = new ClientWebSocket();
                _socket = socket;

                using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    connectCts.CancelAfter(ConnectTimeout);
                    await socket.ConnectAsync(Uri, connectCts.Token).ConfigureAwait(false);
                }

                connectedAt = DateTimeOffset.UtcNow;
                SetConnected(true);
                Log("Connected");
                await OnConnectedAsync(ct).ConfigureAwait(false);
                await ReceiveLoopAsync(socket, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                Log("Connect timed out");
            }
            catch (WebSocketException ex)
            {
                Log($"Socket error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Log($"Unexpected error: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                _socket = null;
                SetConnected(false);
            }

            if (ct.IsCancellationRequested) break;

            // A connection that held for a while resets the backoff; a fast failure doubles it.
            if (connectedAt != DateTimeOffset.MinValue && DateTimeOffset.UtcNow - connectedAt > MaxBackoff) backoff = InitialBackoff;

            Log($"Reconnecting in {backoff.TotalSeconds:0}s");

            try
            {
                await Task.Delay(backoff, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, MaxBackoff.TotalSeconds));
        }

        SetConnected(false);
        Log("Stopped");
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken ct)
    {
        using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var keepAlive = KeepAliveInterval is { } interval ? KeepAliveLoopAsync(interval, sessionCts.Token) : Task.CompletedTask;

        try
        {
            var buffer = new byte[4096];
            var message = new StringBuilder();

            while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(buffer, ct).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Log($"Closed by server: {result.CloseStatus} {result.CloseStatusDescription}");
                    break;
                }

                message.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (!result.EndOfMessage) continue;

                HandleMessage(message.ToString());
                message.Clear();
            }
        }
        finally
        {
            sessionCts.Cancel();

            try
            {
                await keepAlive.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // expected: the session token was cancelled above
            }
        }
    }

    private async Task KeepAliveLoopAsync(TimeSpan interval, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(interval, ct).ConfigureAwait(false);
            await OnKeepAliveAsync(ct).ConfigureAwait(false);
        }
    }

    private void HandleMessage(string message)
    {
        int? bpm;

        try
        {
            bpm = ParseHeartrate(message);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or FormatException or InvalidOperationException or KeyNotFoundException)
        {
            Log($"Could not parse message: {ex.Message}");
            return;
        }

        if (bpm is { } value) HeartrateReceived?.Invoke(value);
    }

    private void SetConnected(bool connected)
    {
        if (_connected == connected) return;

        _connected = connected;
        ConnectionChanged?.Invoke(connected);
    }
}
