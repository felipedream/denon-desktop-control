// DENON Desktop Control
// Copyright (c) 2026 Felipe (@felipedream) - Buin, Santiago de Chile
// Licensed under MIT License
// https://github.com/felipedream/denon-desktop-control

using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DenonRemote.Denon;

/// <summary>
/// Per-channel status extracted from <c>OPINFINS</c> (input signal) and
/// <c>OPINFASP</c> (active speaker) telnet events.
///
/// The receiver encodes three states per channel: 0 = not connected,
/// 1 = connected but idle in the current surround mode, 2 = playing. We track
/// both "present" and "active" because the trim UI should list every speaker
/// the user actually owns, not just the ones the current mode happens to use.
/// </summary>
public partial class ChannelState : ObservableObject
{
    public string Label { get; }

    [ObservableProperty] private bool _isInputActive;
    [ObservableProperty] private bool _isSpeakerActive;

    /// <summary>True when the speaker exists in the setup (state 1 or 2).</summary>
    [ObservableProperty] private bool _isSpeakerPresent;

    /// <summary>Per-channel level trim in dB (-12..+12, half-step precision).</summary>
    [ObservableProperty] private double _levelDb;

    /// <summary>
    /// Set when the app sends a trim command. While this window is open the
    /// parser ignores the receiver's echo, which otherwise fights the slider
    /// the user is dragging and makes it jump on its own.
    /// </summary>
    public DateTime EchoSuppressedUntil { get; private set; } = DateTime.MinValue;

    public void SuppressEcho(TimeSpan window) =>
        EchoSuppressedUntil = DateTime.UtcNow + window;

    public bool IsEchoSuppressed => DateTime.UtcNow < EchoSuppressedUntil;

    public ChannelState(string label) => Label = label;
}
