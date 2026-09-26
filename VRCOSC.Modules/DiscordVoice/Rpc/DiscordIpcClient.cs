// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Forked from Yeusepe's DiscordOSC (https://github.com/Yeusepe/Yeusepes-Modules, GPL-3.0),
// RPCTools/BaseDiscordClient.cs. Restructured: the original had two independent readers
// (a blocking request/response reader and the event listener) competing for the same pipe,
// so responses could be consumed by the wrong side. This client has ONE reader loop that
// routes frames by nonce to pending requests and everything else to EventReceived.
//
// Wire format (both directions): [int32 opcode][int32 length][utf8 json]
//   0 HANDSHAKE, 1 FRAME, 2 CLOSE, 3 PING, 4 PONG

using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Bluscream.Modules.DiscordVoice.Rpc;

/// <summary>A decoded RPC frame; <see cref="IsError"/> when Discord answered with evt ERROR.</summary>
public sealed class RpcResponse : IDisposable
{
    private readonly JsonDocument _doc;
    public JsonElement Root => _doc.RootElement;
    public JsonElement Data => Root.TryGetProperty("data", out var d) ? d : default;
    public bool IsError => Root.TryGetProperty("evt", out var e) && e.ValueKind == JsonValueKind.String && e.GetString() == "ERROR";
    public int ErrorCode => IsError && Data.TryGetProperty("code", out var c) && c.TryGetInt32(out var code) ? code : 0;
    public string ErrorMessage => IsError && Data.TryGetProperty("message", out var m) ? m.GetString() ?? string.Empty : string.Empty;
    internal RpcResponse(JsonDocument doc) => _doc = doc;
    public void Dispose() => _doc.Dispose();
}

public sealed class DiscordIpcClient : IDisposable
{
    private const int OpHandshake = 0, OpFrame = 1, OpClose = 2, OpPing = 3, OpPong = 4;
    private static readonly JsonSerializerOptions JsonOptions = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<RpcResponse>> _pending = new();
    private readonly CancellationTokenSource _cts = new();
    private Stream? _stream;
    private IDisposable? _owner;
    private TaskCompletionSource<RpcResponse>? _readyTcs;
    private int _disposed;

    /// <summary>Raised on the reader thread for every DISPATCH frame (evt set, no matching nonce).</summary>
    public event Action<JsonElement>? EventReceived;
    /// <summary>Raised once when the reader loop ends; the exception is null on a clean close.</summary>
    public event Action<Exception?>? Disconnected;

    public string Transport { get; private set; } = "none";
    public bool IsConnected => _stream is not null && Volatile.Read(ref _disposed) == 0;

    public async Task ConnectPipeAsync(string pipeName, TimeSpan timeout, CancellationToken ct)
    {
        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeout);
            await pipe.ConnectAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        Attach(pipe, pipe, $"pipe {pipeName}");
    }

    public async Task ConnectTcpAsync(string host, int port, TimeSpan timeout, CancellationToken ct)
    {
        var tcp = new TcpClient { NoDelay = true };
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeout);
            await tcp.ConnectAsync(host, port, timeoutCts.Token).ConfigureAwait(false);
        }
        catch
        {
            tcp.Dispose();
            throw;
        }
        Attach(tcp.GetStream(), tcp, $"tcp {host}:{port}");
    }

    private void Attach(Stream stream, IDisposable owner, string transport)
    {
        _stream = stream;
        _owner = owner;
        Transport = transport;
        _ = Task.Run(ReaderLoopAsync);
    }

    /// <summary>Sends the handshake and waits for Discord's READY dispatch.</summary>
    public async Task<RpcResponse> HandshakeAsync(string clientId, TimeSpan timeout, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<RpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _readyTcs = tcs;
        await WriteAsync(OpHandshake, new { v = 1, client_id = clientId }, ct).ConfigureAwait(false);
        return await AwaitWithTimeout(tcs, timeout, "handshake", ct).ConfigureAwait(false);
    }

    /// <summary>Sends a command and waits for the frame carrying the same nonce.</summary>
    public async Task<RpcResponse> SendAsync(RpcCommand command, TimeSpan timeout, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<RpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[command.Nonce] = tcs;
        try
        {
            await WriteAsync(OpFrame, command, ct).ConfigureAwait(false);
            return await AwaitWithTimeout(tcs, timeout, command.Cmd, ct).ConfigureAwait(false);
        }
        finally
        {
            _pending.TryRemove(command.Nonce, out _);
        }
    }

    /// <summary>Sends a command without waiting for its response; the response frame is dropped.</summary>
    public Task SendFireAndForgetAsync(RpcCommand command, CancellationToken ct) => WriteAsync(OpFrame, command, ct);

    private static async Task<RpcResponse> AwaitWithTimeout(TaskCompletionSource<RpcResponse> tcs, TimeSpan timeout, string what, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);
        try
        {
            return await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"Discord did not answer {what} within {timeout.TotalSeconds:0.#}s.");
        }
    }

    private async Task WriteAsync(int opcode, object payload, CancellationToken ct)
    {
        var stream = _stream ?? throw new InvalidOperationException("Not connected to Discord.");
        var body = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        var frame = new byte[8 + body.Length];
        BitConverter.TryWriteBytes(frame.AsSpan(0, 4), opcode);
        BitConverter.TryWriteBytes(frame.AsSpan(4, 4), body.Length);
        body.CopyTo(frame, 8);

        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await stream.WriteAsync(frame, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ReaderLoopAsync()
    {
        Exception? failure = null;
        try
        {
            var header = new byte[8];
            while (!_cts.IsCancellationRequested)
            {
                if (!await ReadExactlyAsync(header).ConfigureAwait(false)) break;
                var opcode = BitConverter.ToInt32(header, 0);
                var length = BitConverter.ToInt32(header, 4);
                if (length < 0 || length > 16 * 1024 * 1024) throw new IOException($"Implausible frame length {length}.");

                var body = new byte[length];
                if (!await ReadExactlyAsync(body).ConfigureAwait(false)) break;
                if (!await HandleFrameAsync(opcode, body).ConfigureAwait(false)) break;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { failure = ex; }

        FailPending(failure);
        Disconnected?.Invoke(failure);
    }

    /// <returns>false when the peer sent CLOSE.</returns>
    private async Task<bool> HandleFrameAsync(int opcode, byte[] body)
    {
        switch (opcode)
        {
            case OpPing:
                await WriteRawAsync(OpPong, body).ConfigureAwait(false);
                return true;
            case OpClose:
                return false;
            case OpPong:
            case OpHandshake:
                return true;
            case OpFrame:
                Dispatch(body);
                return true;
            default:
                return true;
        }
    }

    private void Dispatch(byte[] body)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(body); }
        catch (JsonException) { return; }

        var root = doc.RootElement;
        var nonce = root.TryGetProperty("nonce", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
        var evt = root.TryGetProperty("evt", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;

        if (nonce is not null && _pending.TryRemove(nonce, out var tcs))
        {
            if (!tcs.TrySetResult(new RpcResponse(doc))) doc.Dispose();
            return;
        }

        if (evt == "READY" && _readyTcs is { } ready && ready.TrySetResult(new RpcResponse(doc)))
        {
            _readyTcs = null;
            return;
        }

        using (doc)
        {
            if (evt is not null) EventReceived?.Invoke(root.Clone());
        }
    }

    private async Task WriteRawAsync(int opcode, byte[] body)
    {
        var stream = _stream;
        if (stream is null) return;
        var frame = new byte[8 + body.Length];
        BitConverter.TryWriteBytes(frame.AsSpan(0, 4), opcode);
        BitConverter.TryWriteBytes(frame.AsSpan(4, 4), body.Length);
        body.CopyTo(frame, 8);
        await _writeLock.WaitAsync(_cts.Token).ConfigureAwait(false);
        try { await stream.WriteAsync(frame, _cts.Token).ConfigureAwait(false); }
        finally { _writeLock.Release(); }
    }

    private async Task<bool> ReadExactlyAsync(byte[] buffer)
    {
        var stream = _stream;
        if (stream is null) return false;
        var read = 0;
        while (read < buffer.Length)
        {
            var got = await stream.ReadAsync(buffer.AsMemory(read), _cts.Token).ConfigureAwait(false);
            if (got == 0) return false;
            read += got;
        }
        return true;
    }

    private void FailPending(Exception? cause)
    {
        var error = cause ?? new IOException("Discord IPC connection closed.");
        foreach (var key in _pending.Keys)
        {
            if (_pending.TryRemove(key, out var tcs)) tcs.TrySetException(error);
        }
        _readyTcs?.TrySetException(error);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _cts.Cancel();
        try { _stream?.Dispose(); } catch (IOException) { }
        try { _owner?.Dispose(); } catch (IOException) { }
        FailPending(null);
        _cts.Dispose();
        _writeLock.Dispose();
    }
}
