// DENON Desktop Control
// Copyright (c) 2026 Felipe (@felipedream) - Buin, Santiago de Chile
// Licensed under MIT License
// https://github.com/felipedream/denon-desktop-control

using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DenonRemote.Denon;
using DenonRemote.Discovery;
using DenonRemote.Services;

namespace DenonRemote.ViewModels;

/// <summary>Root view model. Owns connection state and the discovered device list.</summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ReceiverService _receiver;
    private readonly SsdpDiscoveryService _discovery;
    private readonly AppSettings _settings;
    private readonly AutoUpdateService _updater;

    public ReceiverState State => _receiver.State;
    public AppSettings Settings => _settings;
    public AutoUpdateService Updater => _updater;

    public ObservableCollection<ReceiverDescriptor> Devices { get; } = new();
    public ObservableCollection<SourceTile> SourceTiles { get; } = new();
    public ObservableCollection<KnownDevice> RecentDevices { get; } = new();

    [ObservableProperty] private ReceiverDescriptor? _selectedDevice;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _manualHost = "";
    [ObservableProperty] private UpdateInfo? _availableUpdate;

    public MainViewModel(ReceiverService receiver, SsdpDiscoveryService discovery, AppSettings settings, AutoUpdateService updater)
    {
        _receiver = receiver;
        _discovery = discovery;
        _settings = settings;
        _updater = updater;
        _manualHost = settings.LastHost ?? "";

        // Seed the tiles used by the Sources page. Only the sources the AVR
        // has not hidden are shown; renamed inputs preserve the user label.
        foreach (var kv in DenonProtocol.DefaultSources)
            SourceTiles.Add(new SourceTile(kv.Key, kv.Value));

        SyncRecentDevices();
        // Connected fires from a background thread (ReceiverService.ConnectAsync),
        // so we hop to the UI dispatcher before mutating the observable collection.
        _receiver.Connected += (_, _) => System.Windows.Application.Current?.Dispatcher.Invoke(SyncRecentDevices);

        // Volume slider: when the user drags it the binding changes State.Main.Volume.
        // We listen to that change and send the command to the AVR with debounce.
        State.Main.PropertyChanged += OnMainZonePropertyChanged;
    }

    private System.Threading.Timer? _volumeDebounce;

    private void OnMainZonePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Denon.ZoneState.Volume)) return;
        // Clamp to AVR's real max (even though slider goes to 150 visually)
        var vol = Math.Max(0, Math.Min(State.Main.Volume, State.Main.VolumeMax));
        
        // Debounce: wait 300ms after last change before sending
        _volumeDebounce?.Dispose();
        _volumeDebounce = new System.Threading.Timer(_ =>
        {
            _ = _receiver.SetVolumeAsync(vol);
        }, null, 300, System.Threading.Timeout.Infinite);
    }

    private void SyncRecentDevices()
    {
        RecentDevices.Clear();
        foreach (var d in _settings.KnownDevices)
            RecentDevices.Add(d);
    }

    /// <summary>
    /// Called by <c>App.xaml.cs</c> at startup. Tries to reconnect to the last
    /// known device without blocking the UI thread; while waiting the user can
    /// already trigger a discovery scan.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Auto-reconnect uses the fast HTTP probe against the last known host.
        // It runs before discovery so that a returning user gets a working
        // connection in ~300 ms rather than waiting for the 3 s SSDP scan.
        if (_settings.AutoConnect && !string.IsNullOrWhiteSpace(_settings.LastHost))
        {
            StatusMessage = $"Reconnecting to {_settings.LastFriendlyName ?? _settings.LastHost}â€¦";
            var probed = await _discovery.ProbeAsync(_settings.LastHost);
            if (probed is not null)
            {
                await ConnectAsyncInternal(probed);
            }
            else
            {
                StatusMessage = "Last device is offline. Run discovery or add it manually.";
            }
        }

        // Kick off discovery in the background so the Devices page has a
        // populated list ready when the user opens it.
        _ = RefreshDevicesAsync();

        // Auto-update check
        if (_settings.AutoUpdate)
        {
            var update = await _updater.CheckAsync();
            if (update is not null) AvailableUpdate = update;
        }
    }

    [RelayCommand]
    private async Task RefreshDevicesAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Searching the network for Denon / Marantz devicesâ€¦";
            var found = await _discovery.DiscoverAsync(TimeSpan.FromSeconds(3));
            Devices.Clear();
            foreach (var d in found) Devices.Add(d);
            if (Devices.Count == 0)
                StatusMessage = "No receivers found. Add the IP manually.";
            else
                StatusMessage = $"Found {Devices.Count} device(s).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (SelectedDevice is null) return;
        await ConnectAsyncInternal(SelectedDevice);
    }

    [RelayCommand]
    private async Task AddManualAsync()
    {
        var host = (ManualHost ?? "").Trim();
        if (string.IsNullOrEmpty(host)) return;
        IsBusy = true;
        try
        {
            StatusMessage = $"Probing {host}â€¦";
            var probed = await _discovery.ProbeAsync(host);
            if (probed is null)
            {
                StatusMessage = $"Nothing answered at {host}. Check the IP and try again.";
                return;
            }
            if (!Devices.Contains(probed)) Devices.Add(probed);
            SelectedDevice = probed;
            await ConnectAsyncInternal(probed);
        }
        finally { IsBusy = false; }
    }

    private async Task ConnectAsyncInternal(ReceiverDescriptor d)
    {
        IsBusy = true;
        try
        {
            StatusMessage = $"Connecting to {d.FriendlyName ?? d.Model} at {d.Host}â€¦";
            var ok = await _receiver.ConnectAsync(d);
            StatusMessage = ok
                ? $"Connected Â· {State.FriendlyName}"
                : $"Could not reach {d.Host}. Is Network Standby enabled?";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task PowerToggleAsync() => await _receiver.TogglePowerAsync();

    [RelayCommand]
    private async Task MuteToggleAsync() => await _receiver.ToggleMuteAsync();

    [RelayCommand]
    private async Task VolumeUpAsync() => await _receiver.VolumeUpAsync();

    [RelayCommand]
    private async Task VolumeDownAsync() => await _receiver.VolumeDownAsync();

    [RelayCommand]
    private async Task SelectSourceAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        await _receiver.SelectSourceAsync(token);
    }

    [RelayCommand]
    private async Task SetSurroundAsync(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode)) return;
        await _receiver.SetSurroundAsync(mode);
    }

    /// <summary>Sends a per-channel trim to the AVR (called by the dashboard popup).</summary>
    public Task SetChannelTrimAsync(string channel, double db) =>
        _receiver.SetChannelLevelAsync(channel, db);

    /// <summary>One-click connect from the sidebar shortcut list.</summary>
    [RelayCommand]
    private async Task ConnectToRecentAsync(KnownDevice? device)
    {
        if (device is null || string.IsNullOrWhiteSpace(device.Host)) return;
        StatusMessage = $"Connecting to {device.FriendlyName}â€¦";
        var probed = await _discovery.ProbeAsync(device.Host);
        var target = probed ?? new ReceiverDescriptor(
            device.Host, "Denon", device.Model, device.FriendlyName, "");
        await ConnectAsyncInternal(target);
    }
}

/// <summary>Lightweight tile for the Sources page â€” token + caption.</summary>
public sealed record SourceTile(string Token, string Caption);

