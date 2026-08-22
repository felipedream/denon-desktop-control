// DENON Desktop Control
// Copyright (c) 2026 Felipe (@felipedream) - Buin, Santiago de Chile
// Licensed under MIT License
// https://github.com/felipedream/denon-desktop-control

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using DenonRemote.Denon;
using DenonRemote.Discovery;

namespace DenonRemote.Services;

/// <summary>
/// Single facade every view model talks to. Owns the telnet connection, the
/// HTTP fallback and the observable state. All state mutations are marshalled
/// onto the UI dispatcher so bindings always fire on the right thread â€” that's
/// the difference between the status bar lighting up and staying grey.
/// </summary>
public sealed class ReceiverService : IAsyncDisposable
{
    private readonly AppSettings _settings;

    private DenonTelnetClient? _telnet;
    private DenonHttpClient? _http;
    private DenonEventParser? _parser;
    private CancellationTokenSource? _pollCts;

    public ReceiverState State { get; } = new();

    public event EventHandler? StateChanged;
    public event EventHandler? Connected;   // fired once per successful ConnectAsync

    public ReceiverService(AppSettings settings) => _settings = settings;

    // â”€â”€ Connection â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public async Task<bool> ConnectAsync(ReceiverDescriptor descriptor, CancellationToken ct = default)
    {
        await DisconnectAsync().ConfigureAwait(false);

        // Identity: pushed to UI first so the sidebar and status bar update
        // instantly, even before telnet negotiates.
        Ui(() =>
        {
            State.IpAddress = descriptor.Host;
            State.ModelName = descriptor.Model;
            State.Brand = descriptor.Manufacturer;
            State.FriendlyName = string.IsNullOrWhiteSpace(descriptor.FriendlyName)
                ? descriptor.Model
                : descriptor.FriendlyName;
        });

        _telnet = new DenonTelnetClient(descriptor.Host);
        _http = new DenonHttpClient(descriptor.Host);
        _parser = new DenonEventParser(State);
        _telnet.LineReceived += (_, line) => _parser.Feed(line);
        _telnet.ConnectionChanged += (_, ok) => Ui(() =>
        {
            // Telnet dropping doesn't mean we lost the receiver â€” HTTP might
            // still respond. So we only flag as disconnected when both are
            // unreachable (checked at query time by SendOrHttp).
            if (ok) State.IsConnected = true;
            StateChanged?.Invoke(this, EventArgs.Empty);
        });

        var telnetOk = await _telnet.ConnectAsync(ct).ConfigureAwait(false);
        bool httpOk = false;
        if (telnetOk)
        {
            await _telnet.SendManyAsync(
                "PW?", "MV?", "MU?", "SI?", "MS?",
                "PSBAS ?", "PSTRE ?", "PSTONE CTRL ?", "PSSWL ?",
                "SSINFAISFSV ?", "ECO?",
                "Z2?", "NSFRN ?",
                "CV?",
                "SSFUN ?", "SSSOD ?"
            ).ConfigureAwait(false);
        }
        else
        {
            httpOk = await HydrateFromHttpAsync(ct).ConfigureAwait(false);
        }

        var success = telnetOk || httpOk;
        Ui(() => State.IsConnected = success);
        if (success)
        {
            _settings.RememberDevice(descriptor.Host,
                                     descriptor.FriendlyName,
                                     descriptor.Model);
            _settings.LastHost = descriptor.Host;
            _settings.LastFriendlyName = descriptor.FriendlyName;
            _settings.Save();
            Connected?.Invoke(this, EventArgs.Empty);
        }

        StartResilienceLoop();
        return success;
    }

    public async Task DisconnectAsync()
    {
        _pollCts?.Cancel();
        _pollCts = null;
        if (_telnet is not null)
        {
            await _telnet.DisposeAsync().ConfigureAwait(false);
            _telnet = null;
        }
        Ui(() => State.IsConnected = false);
    }

    private async Task<bool> HydrateFromHttpAsync(CancellationToken ct)
    {
        if (_http is null) return false;
        var status = await _http.GetMainStatusAsync(ct).ConfigureAwait(false);
        if (status is null) return false;
        Ui(() =>
        {
            State.Main.IsOn = status.Value.Power;
            State.Main.Source = status.Value.Input;
            State.Main.SourceFriendly = status.Value.Input;
            State.Main.Volume = status.Value.Volume;
            State.Main.IsMuted = status.Value.Mute;
        });
        return true;
    }

    /// <summary>
    /// Keeps the socket alive and re-hydrates state after brief drops. Runs on
    /// a background thread; every state touch is dispatched to the UI.
    /// </summary>
    private void StartResilienceLoop()
    {
        _pollCts = new CancellationTokenSource();
        var token = _pollCts.Token;
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try { await Task.Delay(TimeSpan.FromSeconds(15), token).ConfigureAwait(false); }
                catch { return; }
                if (_telnet is null) return;
                if (!_telnet.IsConnected)
                {
                    var ok = await _telnet.ConnectAsync(token).ConfigureAwait(false);
                    if (ok)
                        await _telnet.SendManyAsync("PW?", "MV?", "MU?", "SI?", "MS?", "Z2?");
                    else
                        await HydrateFromHttpAsync(token).ConfigureAwait(false);
                }
                else
                {
                    await _telnet.SendAsync("PW?", token).ConfigureAwait(false);
                }
            }
        }, token);
    }

    // â”€â”€ Commands â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public Task PowerOnAsync() => SendOrHttp("PWON");
    public Task StandbyAsync() => SendOrHttp("PWSTANDBY");
    public async Task TogglePowerAsync()
    {
        if (State.Main.IsOn) await StandbyAsync();
        else                 await PowerOnAsync();
    }

    public Task MuteAsync(bool on) => SendOrHttp(on ? "MUON" : "MUOFF");
    public Task ToggleMuteAsync() => MuteAsync(!State.Main.IsMuted);

    public Task SetVolumeAsync(double value) => SendOrHttp("MV" + DenonProtocol.FormatVolume(value));
    public Task VolumeUpAsync() => SendOrHttp("MVUP");
    public Task VolumeDownAsync() => SendOrHttp("MVDOWN");

    public Task SelectSourceAsync(string token) => SendOrHttp("SI" + token);
    public Task SetSurroundAsync(string mode) => SendOrHttp("MS" + mode);

    public Task SetBassAsync(double db)
    {
        var raw = 50 + (int)Math.Round(Clamp(db, -6, 6));
        return SendOrHttp("PSBAS " + raw);
    }

    public Task SetTrebleAsync(double db)
    {
        var raw = 50 + (int)Math.Round(Clamp(db, -6, 6));
        return SendOrHttp("PSTRE " + raw);
    }

    public Task SetSubwooferAsync(double db)
    {
        var raw = 50 + (int)Math.Round(Clamp(db, -12, 12));
        return SendOrHttp("PSSWL " + raw);
    }

    public Task SetToneControlAsync(bool on) => SendOrHttp("PSTONE CTRL " + (on ? "ON" : "OFF"));

    /// <summary>
    /// Adjusts an individual channel's trim on the AVR. <paramref name="db"/>
    /// is clamped to â€“12..+12 dB with 0.5 dB precision (protocol tokens 38..62).
    /// </summary>
    public Task SetChannelLevelAsync(string channel, double db)
    {
        if (!DenonProtocol.ChannelSupportsTrim(channel)) return Task.CompletedTask;
        var raw = 50 + Math.Round(Clamp(db, -12, 12) * 2, MidpointRounding.AwayFromZero) / 2.0;
        var whole = (int)Math.Floor(raw);
        var half = raw - whole >= 0.25;
        var token = half ? $"{whole:00}5" : $"{whole:00}";
        return SendOrHttp($"CV{channel} {token}");
    }

    public Task Zone2PowerAsync(bool on) => SendOrHttp(on ? "Z2ON" : "Z2OFF");
    public Task Zone2MuteAsync(bool on) => SendOrHttp("Z2" + (on ? "MUON" : "MUOFF"));
    public Task Zone2VolumeAsync(double v) => SendOrHttp("Z2" + DenonProtocol.FormatVolume(v));
    public Task Zone2SourceAsync(string src) => SendOrHttp("Z2" + src);

    private async Task SendOrHttp(string cmd)
    {
        if (_telnet is not null && _telnet.IsConnected)
        {
            await _telnet.SendAsync(cmd).ConfigureAwait(false);
            return;
        }
        if (_http is not null)
            await _http.SendDirectAsync(cmd).ConfigureAwait(false);
    }

    private static double Clamp(double v, double min, double max) =>
        v < min ? min : v > max ? max : v;

    /// <summary>Marshal an action onto the WPF UI thread.</summary>
    private static void Ui(Action action)
    {
        var app = Application.Current;
        if (app is null) return;
        if (app.Dispatcher.CheckAccess()) action();
        else app.Dispatcher.Invoke(action);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
    }
}

