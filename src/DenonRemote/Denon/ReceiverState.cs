// DENON Desktop Control
// Copyright (c) 2026 Felipe (@felipedream) - Buin, Santiago de Chile
// Licensed under MIT License
// https://github.com/felipedream/denon-desktop-control

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DenonRemote.Denon;

/// <summary>
/// Full, observable snapshot of an AVR. Owned by <see cref="ReceiverService"/>,
/// consumed by every view model. This is a plain observable object so the UI
/// can bind to its properties directly through DataContext / x:Bind.
/// </summary>
public partial class ReceiverState : ObservableObject
{
    // â”€â”€ Identity â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    [ObservableProperty] private string _friendlyName = "AVR";
    [ObservableProperty] private string _modelName = "";
    [ObservableProperty] private string _brand = "Denon";
    [ObservableProperty] private string _ipAddress = "";
    [ObservableProperty] private bool _isConnected;

    // â”€â”€ Zones â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public ZoneState Main { get; } = new(ZoneId.Main, "Main");
    public ZoneState Zone2 { get; } = new(ZoneId.Zone2, "Zone 2");

    // â”€â”€ Sound â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    [ObservableProperty] private string _surroundMode = "-";
    [ObservableProperty] private string _audioFormat = "-";
    [ObservableProperty] private string _sampleRate = "-";
    [ObservableProperty] private double _bass = 0;         // -12..+12 dB
    [ObservableProperty] private double _treble = 0;       // -12..+12 dB
    [ObservableProperty] private double _subwooferLevel = 0; // -12..+12 dB
    [ObservableProperty] private bool _toneControlEnabled;
    [ObservableProperty] private string _ecoMode = "Auto";

    // â”€â”€ Channel matrix â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    /// <summary>Ordered channels (FL, FR, C, SW, SL, SR ...) used by the UI grid.</summary>
    public ObservableCollection<ChannelState> Channels { get; }

    // â”€â”€ Sources / inputs â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    /// <summary>User-facing source list â€” filtered to entries the receiver has not hidden.</summary>
    public ObservableCollection<SourceEntry> Sources { get; } = new();

    // â”€â”€ Now Playing (HEOS) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    [ObservableProperty] private string _playingTitle = "";
    [ObservableProperty] private string _playingArtist = "";
    [ObservableProperty] private string _playingAlbum = "";
    [ObservableProperty] private string _playingArtUrl = "";
    [ObservableProperty] private bool _isPlaying;

    public ReceiverState()
    {
        Channels = new ObservableCollection<ChannelState>(
            DenonProtocol.SignalChannels.Select(c => new ChannelState(c)));
    }

    public ChannelState? Channel(string label) =>
        Channels.FirstOrDefault(c => c.Label == label);
}

/// <summary>Entry in the source list. Value is the token sent to the AVR (SI ...).</summary>
public partial class SourceEntry : ObservableObject
{
    [ObservableProperty] private string _token = "";
    [ObservableProperty] private string _display = "";
    [ObservableProperty] private string _rename = "";

    public string Caption => string.IsNullOrWhiteSpace(Rename) ? Display : Rename;
}

