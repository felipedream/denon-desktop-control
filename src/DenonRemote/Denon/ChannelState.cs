// DENON Desktop Control
// Copyright (c) 2026 Felipe (@felipedream) - Buin, Santiago de Chile
// Licensed under MIT License
// https://github.com/felipedream/denon-desktop-control

using CommunityToolkit.Mvvm.ComponentModel;

namespace DenonRemote.Denon;

/// <summary>
/// Per-channel status extracted from <c>OPINFINS</c> (input signal)
/// and <c>OPINFASP</c> (active speaker) telnet events. The UI binds
/// its channel grid directly to this observable object.
/// </summary>
public partial class ChannelState : ObservableObject
{
    public string Label { get; }

    [ObservableProperty] private bool _isInputActive;
    [ObservableProperty] private bool _isSpeakerActive;

    /// <summary>Per-channel level trim in dB (â€“12..+12, half-step precision).</summary>
    [ObservableProperty] private double _levelDb;

    public ChannelState(string label) => Label = label;
}

