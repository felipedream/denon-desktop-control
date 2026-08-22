// DENON Desktop Control
// Copyright (c) 2026 Felipe (@felipedream) - Buin, Santiago de Chile
// Licensed under MIT License
// https://github.com/felipedream/denon-desktop-control

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DenonRemote.Denon;

/// <summary>
/// Persistent Denon/Marantz telnet client on port 23.
///
/// The Denon AVR control protocol is line based: commands and replies are
/// terminated with a single CR (\r). Once connected the receiver keeps
/// pushing unsolicited notifications for every state change, no matter what
/// caused it (physical remote, front panel, HEOS app, Zone 2, etc.).
/// That behaviour is what makes this class the primary data source for the UI.
/// </summary>
public sealed class DenonTelnetClient : IAsyncDisposable
{
    private const int Port = 23;
    private const int SendCooldownMs = 40;

    private readonly string _host;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentQueue<string> _pending = new();

    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private Task? _readerLoop;
    private volatile bool _connected;

    public event EventHandler<string>? LineReceived;
    public event EventHandler<bool>? ConnectionChanged;

    public string Host => _host;
    public bool IsConnected => _connected;

    public DenonTelnetClient(string host) => _host = host;

    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        try
        {
            _tcp = new TcpClient { NoDelay = true };
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            using var joined = CancellationTokenSource.CreateLinkedTokenSource(linked.Token, timeout.Token);
            await _tcp.ConnectAsync(_host, Port, joined.Token).ConfigureAwait(false);
            _stream = _tcp.GetStream();
            _connected = true;
            ConnectionChanged?.Invoke(this, true);
            _readerLoop = Task.Run(() => ReadLoopAsync(_cts.Token));
            return true;
        }
        catch
        {
            _connected = false;
            ConnectionChanged?.Invoke(this, false);
            _tcp?.Dispose();
            _tcp = null;
            _stream = null;
            return false;
        }
    }

    /// <summary>
    /// Send a single command. Returns immediately; responses arrive
    /// asynchronously through <see cref="LineReceived"/>.
    /// </summary>
    public async Task SendAsync(string command, CancellationToken ct = default)
    {
        if (!_connected || _stream is null) return;
        await _sendLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var bytes = Encoding.ASCII.GetBytes(command.TrimEnd('\r') + "\r");
            await _stream.WriteAsync(bytes, ct).ConfigureAwait(false);
            await Task.Delay(SendCooldownMs, ct).ConfigureAwait(false); // AVR needs breathing room
        }
        catch
        {
            MarkDisconnected();
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>Send a burst of commands sequentially.</summary>
    public async Task SendManyAsync(params string[] commands)
    {
        foreach (var c in commands)
            await SendAsync(c).ConfigureAwait(false);
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        if (_stream is null) return;
        var buf = new byte[4096];
        var sb = new StringBuilder();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                int n;
                try
                {
                    n = await _stream.ReadAsync(buf, ct).ConfigureAwait(false);
                }
                catch (IOException) { break; }
                catch (OperationCanceledException) { break; }
                if (n <= 0) break;

                for (int i = 0; i < n; i++)
                {
                    char c = (char)buf[i];
                    if (c == '\r' || c == '\n')
                    {
                        if (sb.Length > 0)
                        {
                            var line = sb.ToString();
                            sb.Clear();
                            LineReceived?.Invoke(this, line);
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
            }
        }
        finally
        {
            MarkDisconnected();
        }
    }

    private void MarkDisconnected()
    {
        if (!_connected) return;
        _connected = false;
        ConnectionChanged?.Invoke(this, false);
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try { if (_readerLoop is not null) await _readerLoop.ConfigureAwait(false); }
        catch { /* ignore */ }
        _stream?.Dispose();
        _tcp?.Dispose();
        _sendLock.Dispose();
        _cts.Dispose();
    }
}

