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
    private readonly Heos.HeosService _heos;

    public ReceiverState State => _receiver.State;
    public AppSettings Settings => _settings;
    public AutoUpdateService Updater => _updater;

    public ObservableCollection<ReceiverDescriptor> Devices { get; } = new();
    public ObservableCollection<SourceTile> SourceTiles { get; } = new();
    public ObservableCollection<KnownDevice> RecentDevices { get; } = new();
    public ObservableCollection<ChannelProfile> Profiles { get; } = new();

    /// <summary>Channels that the receiver reports as physically connected.</summary>
    public ObservableCollection<Denon.ChannelState> ActiveChannels { get; } = new();

    [ObservableProperty] private ReceiverDescriptor? _selectedDevice;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _manualHost = "";
    [ObservableProperty] private UpdateInfo? _availableUpdate;

    public MainViewModel(ReceiverService receiver, SsdpDiscoveryService discovery, AppSettings settings, AutoUpdateService updater, Heos.HeosService heos)
    {
        _receiver = receiver;
        _discovery = discovery;
        _settings = settings;
        _updater = updater;
        _heos = heos;
        _manualHost = settings.LastHost ?? "";

        // Seed the tiles used by the Sources page. Only the sources the AVR
        // has not hidden are shown; renamed inputs preserve the user label.
        foreach (var kv in DenonProtocol.DefaultSources)
            SourceTiles.Add(new SourceTile(kv.Key, kv.Value));

        SyncRecentDevices();
        SyncProfiles();
        // Connected fires from a background thread (ReceiverService.ConnectAsync),
        // so we hop to the UI dispatcher before mutating the observable collection.
        _receiver.Connected += (_, _) => System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            SyncRecentDevices();
            RefreshActiveChannels();
        });

        // Keep the channel list in sync as the AVR reports speaker changes.
        foreach (var ch in State.Channels)
            ch.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(Denon.ChannelState.IsSpeakerPresent))
                    System.Windows.Application.Current?.Dispatcher.Invoke(RefreshActiveChannels);
            };

        // Volume slider: when the user drags it the binding changes State.Main.Volume.
        // We listen to that change and send the command to the AVR with debounce.
        State.Main.PropertyChanged += OnMainZonePropertyChanged;

        // Zone 2 must never outlive the main zone. If Main reports OFF while
        // Zone 2 is still ON, push Z2OFF so the receiver matches expectations.
        State.Main.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName != nameof(Denon.ZoneState.IsOn)) return;
            if (!State.Main.IsOn && State.Zone2.IsOn)
                await _receiver.Zone2PowerAsync(false);
        };

        // Keep the percentage calculation accurate once the receiver reports
        // its configured maximum.
        State.Main.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Denon.ZoneState.VolumeMax))
                Converters.VolumeLabelConverter.Max = State.Main.VolumeMax;
        };

        Converters.VolumeLabelConverter.Unit = _settings.VolumeUnit;
        Converters.VolumeLabelConverter.Max = State.Main.VolumeMax;
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

    private void SyncProfiles()
    {
        Profiles.Clear();
        foreach (var p in _settings.ChannelProfiles)
            Profiles.Add(p);
    }

    /// <summary>
    /// Rebuilds <see cref="ActiveChannels"/> from the speaker matrix so the UI
    /// only shows sliders for speakers that actually exist in the setup.
    /// SW is excluded on purpose: the subwoofer uses its own protocol command
    /// (<c>PSSWL</c>) and already has a dedicated card in the Sound page.
    /// </summary>
    public void RefreshActiveChannels()
    {
        // Use IsSpeakerPresent (state 1 or 2) rather than IsSpeakerActive so the
        // list shows every speaker wired to the receiver. A channel can be
        // connected but idle — e.g. the centre and surrounds while the current
        // mode is plain STEREO — and the user still wants to trim it.
        var active = State.Channels
            .Where(c => c.IsSpeakerPresent
                     && c.Label != "SW"
                     && Denon.DenonProtocol.ChannelSupportsTrim(c.Label))
            .ToList();

        // Only rebuild when the set actually changed, otherwise the ItemsControl
        // flickers and loses slider focus while the user is dragging.
        if (active.Count == ActiveChannels.Count &&
            active.Select(c => c.Label).SequenceEqual(ActiveChannels.Select(c => c.Label)))
            return;

        ActiveChannels.Clear();
        foreach (var c in active) ActiveChannels.Add(c);
    }

    /// <summary>
    /// Flattens every trim back to 0 dB: channels, subwoofer, bass and treble.
    /// Local state is written first so the sliders snap immediately rather than
    /// waiting for the receiver to echo each confirmation.
    /// </summary>
    [RelayCommand]
    private async Task NormalizeChannelsAsync()
    {
        foreach (var ch in ActiveChannels)
        {
            ch.LevelDb = 0;
            await _receiver.SetChannelLevelAsync(ch.Label, 0);
        }

        State.SubwooferLevel = 0;
        State.Bass = 0;
        State.Treble = 0;

        await _receiver.SetSubwooferAsync(0);
        await _receiver.SetBassAsync(0);
        await _receiver.SetTrebleAsync(0);

        StatusMessage = "Niveles normalizados a 0 dB";
    }

    /// <summary>Available units for the volume readout, bound to the Settings combo.</summary>
    public IReadOnlyList<VolumeUnitOption> VolumeUnits { get; } = new[]
    {
        new VolumeUnitOption(VolumeUnit.Absolute, "Valor absoluto (52.5)"),
        new VolumeUnitOption(VolumeUnit.Decibels, "Decibelios (-27.5 dB)"),
        new VolumeUnitOption(VolumeUnit.Percent,  "Porcentaje (56%)")
    };

    public VolumeUnitOption SelectedVolumeUnit
    {
        get => VolumeUnits.FirstOrDefault(u => u.Unit == _settings.VolumeUnit) ?? VolumeUnits[0];
        set
        {
            if (value is null || value.Unit == _settings.VolumeUnit) return;
            _settings.VolumeUnit = value.Unit;
            _settings.Save();
            ApplyVolumeUnit();
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Pushes the chosen unit into the converter and nudges every volume binding
    /// so the labels re-render without needing a restart.
    /// </summary>
    private void ApplyVolumeUnit()
    {
        Converters.VolumeLabelConverter.Unit = _settings.VolumeUnit;
        Converters.VolumeLabelConverter.Max = State.Main.VolumeMax;

        // Force the bindings to re-evaluate by re-setting the same value.
        var v = State.Main.Volume;
        State.Main.Volume = v == 0 ? 0.01 : 0;
        State.Main.Volume = v;
    }

    /// <summary>
    /// Captures the complete sound state — every channel trim, subwoofer, bass,
    /// treble and the tone-control switch — under the next free slot.
    /// </summary>
    [RelayCommand]
    private void SaveProfile()
    {
        var profile = new ChannelProfile
        {
            Name = $"Volumen {_settings.ChannelProfiles.Count + 1}",
            SubwooferLevel = State.SubwooferLevel,
            Bass = State.Bass,
            Treble = State.Treble,
            ToneControlEnabled = State.ToneControlEnabled
        };

        // Store every channel the receiver knows about, not just the ones the
        // current surround mode happens to drive.
        foreach (var ch in State.Channels)
            if (ch.IsSpeakerPresent && Denon.DenonProtocol.ChannelSupportsTrim(ch.Label))
                profile.Levels[ch.Label] = ch.LevelDb;

        _settings.ChannelProfiles.Add(profile);
        _settings.Save();
        SyncProfiles();
        StatusMessage = $"Perfil guardado: {profile.Name}";
    }

    /// <summary>Applies a saved profile to the receiver and to local state.</summary>
    [RelayCommand]
    private async Task LoadProfileAsync(ChannelProfile? profile)
    {
        if (profile is null) return;

        foreach (var kv in profile.Levels)
        {
            var ch = State.Channel(kv.Key);
            if (ch is not null) ch.LevelDb = kv.Value;
            await _receiver.SetChannelLevelAsync(kv.Key, kv.Value);
        }

        State.SubwooferLevel = profile.SubwooferLevel;
        State.Bass = profile.Bass;
        State.Treble = profile.Treble;
        State.ToneControlEnabled = profile.ToneControlEnabled;

        await _receiver.SetSubwooferAsync(profile.SubwooferLevel);
        await _receiver.SetToneControlAsync(profile.ToneControlEnabled);
        await _receiver.SetBassAsync(profile.Bass);
        await _receiver.SetTrebleAsync(profile.Treble);

        StatusMessage = $"Perfil cargado: {profile.Name}";
    }

    /// <summary>Removes a saved profile.</summary>
    [RelayCommand]
    private void DeleteProfile(ChannelProfile? profile)
    {
        if (profile is null) return;
        _settings.ChannelProfiles.Remove(profile);
        _settings.Save();
        SyncProfiles();
        StatusMessage = $"Perfil eliminado: {profile.Name}";
    }

    /// <summary>Writes profile changes (e.g. an inline rename) back to disk.</summary>
    public void PersistProfiles() => _settings.Save();

    // ── HEOS ────────────────────────────────────────────────────────────────

    public Heos.HeosService Heos => _heos;

    [RelayCommand]
    private async Task HeosPlayPauseAsync() => await _heos.PlayPauseAsync();

    [RelayCommand]
    private async Task HeosNextAsync() => await _heos.NextAsync();

    [RelayCommand]
    private async Task HeosPreviousAsync() => await _heos.PreviousAsync();

    [RelayCommand]
    private async Task HeosRefreshAsync()
    {
        await _heos.RefreshNowPlayingAsync();
        await _heos.RefreshSourcesAsync();
        await _heos.RefreshAccountAsync();
        await _heos.RefreshQueueAsync();
    }

    [RelayCommand]
    private async Task HeosSignOutAsync() => await _heos.SignOutAsync();

    [RelayCommand]
    private async Task HeosBackAsync() => await _heos.GoBackAsync();

    [RelayCommand]
    private void HeosHome() => _heos.GoHome();

    public Task HeosSearchNowPlayingAsync(string term, string kind) =>
        _heos.SearchFromNowPlayingAsync(term, kind);

    public Task HeosSeekAsync(long ms) => _heos.SeekAsync(ms);

    [RelayCommand]
    private async Task HeosClearQueueAsync() => await _heos.ClearQueueAsync();

    // Called from the HEOS page code-behind, where the clicked item is known.
    public Task HeosOpenSourceAsync(Heos.MusicSource s) => _heos.OpenSourceAsync(s);
    public Task HeosOpenItemAsync(Heos.BrowseItem i) => _heos.OpenItemAsync(i);
    public Task HeosPlayItemAsync(Heos.BrowseItem i) => _heos.PlayItemAsync(i);
    public Task HeosQueueItemAsync(Heos.BrowseItem i) => _heos.QueueItemAsync(i);
    public Task HeosPlayQueueItemAsync(Heos.QueueItem q) => _heos.PlayQueueItemAsync(q);
    public Task HeosSearchAsync(string scid, string term) => _heos.SearchAsync(scid, term);

    /// <summary>Called by the HEOS page once the user submits the login form.</summary>
    public Task<bool> HeosSignInAsync(string user, string password) =>
        _heos.SignInAsync(user, password);

    /// <summary>Sends a single channel trim (called from the Sound page sliders).</summary>
    public Task SetChannelLevelAsync(string channel, double db) =>
        _receiver.SetChannelLevelAsync(channel, db);

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
            StatusMessage = L.IsSpanish
                ? $"Conectando a {d.FriendlyName ?? d.Model} en {d.Host}..."
                : $"Connecting to {d.FriendlyName ?? d.Model} at {d.Host}...";

            var ok = await _receiver.ConnectAsync(d);

            // HEOS lives on its own socket (port 1255). Connect it in the
            // background so a HEOS failure never blocks receiver control.
            if (ok) _ = _heos.ConnectAsync(d.Host);

            StatusMessage = ok
                ? (L.IsSpanish ? $"Conectado - {State.FriendlyName}"
                               : $"Connected - {State.FriendlyName}")
                : (L.IsSpanish ? $"No se pudo alcanzar {d.Host}. Revisa Network Standby."
                               : $"Could not reach {d.Host}. Is Network Standby enabled?");
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

    /// <summary>
    /// Explicit Zone 2 power toggle. Zone 2 is never enabled implicitly — only
    /// through this command — and it always goes down when Main powers off.
    /// </summary>
    [RelayCommand]
    private async Task ToggleZone2Async()
    {
        if (State.Zone2.IsOn)
            await _receiver.Zone2PowerAsync(false);
        else
            await _receiver.Zone2PowerAsync(true);
    }

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

/// <summary>Combo entry for the volume-unit selector in Settings.</summary>
public sealed record VolumeUnitOption(VolumeUnit Unit, string Caption)
{
    public override string ToString() => Caption;
}


