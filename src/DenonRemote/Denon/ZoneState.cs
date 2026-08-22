// DENON Desktop Control
// Copyright (c) 2026 Felipe (@felipedream) - Buin, Santiago de Chile
// Licensed under MIT License
// https://github.com/felipedream/denon-desktop-control

using CommunityToolkit.Mvvm.ComponentModel;

namespace DenonRemote.Denon;

public enum ZoneId { Main, Zone2, Zone3 }

/// <summary>
/// Observable state for a single zone. Bound directly to the UI.
/// Values are updated by <see cref="ReceiverState"/> as telnet events arrive.
/// </summary>
public partial class ZoneState : ObservableObject
{
    public ZoneId Id { get; }
    public string DisplayName { get; }

    [ObservableProperty] private bool _isOn;
    [ObservableProperty] private bool _isMuted;
    [ObservableProperty] private double _volume;              // 0..99, half steps
    [ObservableProperty] private double _volumeMax = 98;
    [ObservableProperty] private string _source = "";
    [ObservableProperty] private string _sourceFriendly = "";

    public ZoneState(ZoneId id, string displayName)
    {
        Id = id;
        DisplayName = displayName;
    }
}

