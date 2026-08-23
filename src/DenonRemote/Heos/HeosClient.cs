// DENON Desktop Control
// Copyright (c) 2026 Felipe (@felipedream) - Buin, Santiago de Chile
// Licensed under MIT License
// https://github.com/felipedream/denon-desktop-control

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DenonRemote.Heos;

/// <summary>
/// Client for the HEOS CLI protocol (TCP port 1255).
///
/// Architecture: a single background reader loop consumes every line the
/// receiver sends and routes it either to the pending request that asked for
/// it, or to <see cref="EventReceived"/> for unsolicited notifications.
///
/// This matters because HEOS answers some browse commands twice: first with
/// <c>"message": "command under process"</c> and only later with the real
/// payload. A naive request/response reader would take the placeholder as the
/// answer and then desynchronise the stream.
/// </summary>
public sealed class HeosClient : IAsyncDisposable
{
    private const int Port = 1255;

    private readonly string _host;
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pending = new();
    private readonly CancellationTokenSource _cts = new();

    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private Task? _reader;

    public string? PlayerId { get; private set; }
    public bool IsConnected => _tcp?.Connected == true;
    public string? LastError { get; private set; }

    /// <summary>Raised for every <c>event/...</c> line the receiver pushes.</summary>
    public event EventHandler<HeosEvent>? EventReceived;

    public HeosClient(string host) => _host = host;

    // ── Connection ──────────────────────────────────────────────────────────

    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        try
        {
            _tcp = new TcpClient { NoDelay = true };
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(4));
            await _tcp.ConnectAsync(_host, Port, timeout.Token).ConfigureAwait(false);
            _stream = _tcp.GetStream();

            _reader = Task.Run(() => ReadLoopAsync(_cts.Token));

            // Change events give us now-playing transitions and the progress
            // ticks that drive the seek bar.
            await SendAsync("heos://system/register_for_change_events?enable=on", ct)
                .ConfigureAwait(false);

            var players = await SendAsync("heos://player/get_players", ct).ConfigureAwait(false);
            PlayerId = ExtractFirstPlayerId(players);
            return PlayerId is not null;
        }
        catch (Exception ex)
        {
            LastError = $"{ex.GetType().Name}: {ex.Message}";
            _tcp?.Dispose();
            _tcp = null;
            _stream = null;
            return false;
        }
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        if (_stream is null) return;
        var buf = new byte[16384];
        var pending = new StringBuilder();

        try
        {
            while (!ct.IsCancellationRequested)
            {
                int n;
                try { n = await _stream.ReadAsync(buf, ct).ConfigureAwait(false); }
                catch { break; }
                if (n <= 0) break;

                pending.Append(Encoding.UTF8.GetString(buf, 0, n));

                while (true)
                {
                    var text = pending.ToString();
                    var idx = text.IndexOf('\n');
                    if (idx < 0) break;
                    var line = text[..idx].Trim();
                    pending.Remove(0, idx + 1);
                    if (line.Length > 0) Dispatch(line);
                }
            }
        }
        finally
        {
            // Unblock anyone still waiting so callers don't hang on shutdown.
            foreach (var kv in _pending)
                kv.Value.TrySetResult("");
            _pending.Clear();
        }
    }

    private void Dispatch(string line)
    {
        var command = ExtractCommand(line);
        if (command is null) return;

        if (command.StartsWith("event/", StringComparison.Ordinal))
        {
            EventReceived?.Invoke(this, new HeosEvent(command, ExtractMessage(line) ?? "", line));
            return;
        }

        // Placeholder acknowledgements are not the answer; the payload follows.
        var message = ExtractMessage(line) ?? "";
        if (message.Contains("command under process", StringComparison.OrdinalIgnoreCase))
            return;

        if (_pending.TryRemove(command, out var tcs))
            tcs.TrySetResult(line);
    }

    // ── Request plumbing ────────────────────────────────────────────────────

    /// <summary>
    /// Sends a command and waits for the line that answers it. Returns null on
    /// timeout so callers can degrade gracefully.
    /// </summary>
    private async Task<string?> SendAsync(string command, CancellationToken ct = default,
                                          int timeoutSeconds = 8)
    {
        if (_stream is null) return null;

        var key = command.StartsWith("heos://", StringComparison.Ordinal)
            ? command[7..].Split('?')[0]
            : command;

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[key] = tcs;

        await _sendGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var payload = Encoding.ASCII.GetBytes(command + "\r\n");
            await _stream.WriteAsync(payload, ct).ConfigureAwait(false);
        }
        catch
        {
            _pending.TryRemove(key, out _);
            return null;
        }
        finally { _sendGate.Release(); }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            var result = await tcs.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
            return string.IsNullOrEmpty(result) ? null : result;
        }
        catch
        {
            _pending.TryRemove(key, out _);
            return null;
        }
    }

    // ── Now playing ─────────────────────────────────────────────────────────

    public async Task<NowPlaying?> GetNowPlayingAsync(CancellationToken ct = default)
    {
        if (PlayerId is null) return null;
        var json = await SendAsync($"heos://player/get_now_playing_media?pid={PlayerId}", ct)
            .ConfigureAwait(false);
        if (json is null) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("payload", out var p)) return null;
            if (p.ValueKind != JsonValueKind.Object) return null;

            return new NowPlaying(
                Title:     Str(p, "song"),
                Artist:    Str(p, "artist"),
                Album:     Str(p, "album"),
                ImageUrl:  Str(p, "image_url"),
                Station:   Str(p, "station"),
                MediaType: Str(p, "type"),
                QueueId:   Str(p, "qid"));
        }
        catch { return null; }
    }

    public async Task<string?> GetPlayStateAsync(CancellationToken ct = default)
    {
        if (PlayerId is null) return null;
        var json = await SendAsync($"heos://player/get_play_state?pid={PlayerId}", ct)
            .ConfigureAwait(false);
        return json is null ? null : ValueFromMessage(json, "state");
    }

    // ── Transport ───────────────────────────────────────────────────────────

    public Task PlayAsync(CancellationToken ct = default) => SetStateAsync("play", ct);
    public Task PauseAsync(CancellationToken ct = default) => SetStateAsync("pause", ct);
    public Task StopAsync(CancellationToken ct = default) => SetStateAsync("stop", ct);

    private async Task SetStateAsync(string state, CancellationToken ct)
    {
        if (PlayerId is null) return;
        await SendAsync($"heos://player/set_play_state?pid={PlayerId}&state={state}", ct)
            .ConfigureAwait(false);
    }

    public async Task NextAsync(CancellationToken ct = default)
    {
        if (PlayerId is null) return;
        await SendAsync($"heos://player/play_next?pid={PlayerId}", ct).ConfigureAwait(false);
    }

    public async Task PreviousAsync(CancellationToken ct = default)
    {
        if (PlayerId is null) return;
        await SendAsync($"heos://player/play_previous?pid={PlayerId}", ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Seeks to an absolute position using the UPnP AVTransport Seek action.
    ///
    /// The HEOS CLI does not support seek on any known firmware version. The
    /// app HEOS uses the renderer's UPnP endpoint directly for this. The
    /// position is specified as H:MM:SS relative to the start of the track.
    /// </summary>
    public async Task<bool> SeekAsync(long milliseconds, CancellationToken ct = default)
    {
        var ts = TimeSpan.FromMilliseconds(milliseconds);
        var target = $"{(int)ts.TotalHours}:{ts.Minutes:00}:{ts.Seconds:00}";

        var soap = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"" s:encodingStyle=""http://schemas.xmlsoap.org/soap/encoding/"">
<s:Body>
<u:Seek xmlns:u=""urn:schemas-upnp-org:service:AVTransport:1"">
<InstanceID>0</InstanceID>
<Unit>ABS_TIME</Unit>
<Target>{target}</Target>
</u:Seek>
</s:Body>
</s:Envelope>";

        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var request = new System.Net.Http.HttpRequestMessage(
                System.Net.Http.HttpMethod.Post,
                $"http://{_host}:60006/upnp/control/renderer_dvc/AVTransport");
            request.Content = new System.Net.Http.StringContent(soap, Encoding.UTF8, "text/xml");
            request.Headers.TryAddWithoutValidation("SOAPAction",
                "\"urn:schemas-upnp-org:service:AVTransport:1#Seek\"");

            var response = await http.SendAsync(request, ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ── Sources and browsing ────────────────────────────────────────────────

    public Task<string?> GetMusicSourcesRawAsync(CancellationToken ct = default) =>
        SendAsync("heos://browse/get_music_sources", ct);

    /// <summary>Lists the root of a source, or the contents of a container.</summary>
    public Task<string?> BrowseAsync(string sid, string? cid = null, int start = 0, int end = 99,
                                    CancellationToken ct = default)
    {
        var cmd = cid is null
            ? $"heos://browse/browse?sid={sid}&range={start},{end}"
            : $"heos://browse/browse?sid={sid}&cid={Uri.EscapeDataString(cid)}&range={start},{end}";
        return SendAsync(cmd, ct, timeoutSeconds: 18);
    }

    /// <summary>Search criteria supported by a service (artist, album, track...).</summary>
    public Task<string?> GetSearchCriteriaAsync(string sid, CancellationToken ct = default) =>
        SendAsync($"heos://browse/get_search_criteria?sid={sid}", ct);

    public Task<string?> SearchAsync(string sid, string scid, string term,
                                    CancellationToken ct = default) =>
        SendAsync($"heos://browse/search?sid={sid}&search={Uri.EscapeDataString(term)}&scid={scid}",
                  ct, timeoutSeconds: 18);

    // ── Playback of browsed items ───────────────────────────────────────────

    /// <summary>Plays a single track straight away, replacing the queue.</summary>
    public Task PlayStreamAsync(string sid, string cid, string mid, CancellationToken ct = default) =>
        PlayerId is null
            ? Task.CompletedTask
            : SendAsync($"heos://browse/play_stream?pid={PlayerId}&sid={sid}" +
                        $"&cid={Uri.EscapeDataString(cid)}&mid={Uri.EscapeDataString(mid)}", ct);

    /// <summary>Plays a station (TuneIn and other radio services).</summary>
    public Task PlayStationAsync(string sid, string cid, string mid, string name,
                                 CancellationToken ct = default) =>
        PlayerId is null
            ? Task.CompletedTask
            : SendAsync($"heos://browse/play_stream?pid={PlayerId}&sid={sid}" +
                        $"&cid={Uri.EscapeDataString(cid)}&mid={Uri.EscapeDataString(mid)}" +
                        $"&name={Uri.EscapeDataString(name)}", ct);

    /// <summary>
    /// Adds a container or track to the queue.
    /// aid: 1 = play now, 2 = play next, 3 = add to end, 4 = replace queue.
    /// </summary>
    public Task AddToQueueAsync(string sid, string cid, string? mid, int aid = 1,
                               CancellationToken ct = default)
    {
        if (PlayerId is null) return Task.CompletedTask;
        var cmd = $"heos://browse/add_to_queue?pid={PlayerId}&sid={sid}" +
                  $"&cid={Uri.EscapeDataString(cid)}&aid={aid}";
        if (!string.IsNullOrEmpty(mid)) cmd += $"&mid={Uri.EscapeDataString(mid)}";
        return SendAsync(cmd, ct, timeoutSeconds: 12);
    }

    // ── Queue ───────────────────────────────────────────────────────────────

    public Task<string?> GetQueueAsync(int start = 0, int end = 99, CancellationToken ct = default) =>
        PlayerId is null
            ? Task.FromResult<string?>(null)
            : SendAsync($"heos://player/get_queue?pid={PlayerId}&range={start},{end}", ct);

    public Task PlayQueueItemAsync(string qid, CancellationToken ct = default) =>
        PlayerId is null
            ? Task.CompletedTask
            : SendAsync($"heos://player/play_queue?pid={PlayerId}&qid={qid}", ct);

    public Task ClearQueueAsync(CancellationToken ct = default) =>
        PlayerId is null
            ? Task.CompletedTask
            : SendAsync($"heos://player/clear_queue?pid={PlayerId}", ct);

    // ── Account ─────────────────────────────────────────────────────────────

    public string? LastRawAccount { get; private set; }

    public async Task<string?> CheckAccountAsync(CancellationToken ct = default)
    {
        var json = await SendAsync("heos://system/check_account", ct).ConfigureAwait(false);
        LastRawAccount = json;
        if (json is null) return null;

        var msg = ExtractMessage(json) ?? "";
        if (!msg.Contains("signed_in", StringComparison.OrdinalIgnoreCase)) return null;
        foreach (var part in msg.Split('&'))
            if (part.StartsWith("un=", StringComparison.OrdinalIgnoreCase))
                return part[3..];
        return null;
    }

    /// <summary>
    /// Signs in to a HEOS account so the streaming services become available.
    /// The HEOS protocol sends credentials in clear text over the LAN — a
    /// limitation of the protocol itself. The password is used for this single
    /// request and never stored.
    /// </summary>
    public async Task<(bool Success, string Message)> SignInAsync(
        string username, string password, CancellationToken ct = default)
    {
        var cmd = $"heos://system/sign_in?un={Uri.EscapeDataString(username)}" +
                  $"&pw={Uri.EscapeDataString(password)}";
        var json = await SendAsync(cmd, ct, timeoutSeconds: 15).ConfigureAwait(false);
        if (json is null) return (false, "Sin respuesta del receptor");

        try
        {
            using var doc = JsonDocument.Parse(json);
            var heos = doc.RootElement.GetProperty("heos");
            var ok = (heos.GetProperty("result").GetString() ?? "")
                .Equals("success", StringComparison.OrdinalIgnoreCase);
            var message = heos.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
            return (ok, ok ? "Sesion iniciada" : DescribeError(message));
        }
        catch { return (false, "Respuesta no valida"); }
    }

    public async Task<bool> SignOutAsync(CancellationToken ct = default)
    {
        var json = await SendAsync("heos://system/sign_out", ct).ConfigureAwait(false);
        if (json is null) return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return (doc.RootElement.GetProperty("heos").GetProperty("result").GetString() ?? "")
                .Equals("success", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    // ── JSON helpers ────────────────────────────────────────────────────────

    private static string? ExtractCommand(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            return doc.RootElement.GetProperty("heos").GetProperty("command").GetString();
        }
        catch { return null; }
    }

    private static string? ExtractMessage(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var heos = doc.RootElement.GetProperty("heos");
            return heos.TryGetProperty("message", out var m) ? m.GetString() : null;
        }
        catch { return null; }
    }

    /// <summary>Pulls a key out of the ampersand-separated message field.</summary>
    public static string? ValueFromMessage(string json, string key)
    {
        var msg = ExtractMessage(json);
        if (msg is null) return null;
        foreach (var part in msg.Split('&'))
            if (part.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                return part[(key.Length + 1)..];
        return null;
    }

    private static string? ExtractFirstPlayerId(string? json)
    {
        if (json is null) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var payload = doc.RootElement.GetProperty("payload");
            if (payload.ValueKind != JsonValueKind.Array || payload.GetArrayLength() == 0) return null;
            return payload[0].GetProperty("pid").ToString();
        }
        catch { return null; }
    }

    private static string DescribeError(string message)
    {
        foreach (var part in message.Split('&'))
            if (part.StartsWith("text=", StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(part[5..]).Replace('+', ' ');
        return "No se pudo iniciar sesion";
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? v.ToString() : "";

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try { if (_reader is not null) await _reader.ConfigureAwait(false); } catch { }
        _stream?.Dispose();
        _tcp?.Dispose();
        _sendGate.Dispose();
        _cts.Dispose();
    }
}

/// <summary>Snapshot of what the receiver is currently playing.</summary>
public sealed record NowPlaying(
    string Title,
    string Artist,
    string Album,
    string ImageUrl,
    string Station,
    string MediaType,
    string QueueId);

/// <summary>An unsolicited notification pushed by the receiver.</summary>
public sealed record HeosEvent(string Command, string Message, string RawLine);
