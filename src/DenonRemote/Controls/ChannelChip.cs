// DENON Desktop Control
// Copyright (c) 2026 Felipe (@felipedream) - Buin, Santiago de Chile
// Licensed under MIT License
// https://github.com/felipedream/denon-desktop-control

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DenonRemote.Denon;

namespace DenonRemote.Controls;

/// <summary>
/// Small pill that represents a single channel in the dashboard matrix.
/// Renders differently for the input-signal grid and the active-speaker grid,
/// and exposes a click event so the parent can pop up the trim slider.
/// </summary>
public sealed class ChannelChip : Control
{
    static ChannelChip()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ChannelChip),
            new FrameworkPropertyMetadata(typeof(ChannelChip)));
    }

    public static readonly DependencyProperty ChannelProperty = DependencyProperty.Register(
        nameof(Channel), typeof(ChannelState), typeof(ChannelChip));

    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
        nameof(Mode), typeof(ChipMode), typeof(ChannelChip),
        new PropertyMetadata(ChipMode.InputSignal));

    public ChannelState? Channel
    {
        get => (ChannelState?)GetValue(ChannelProperty);
        set => SetValue(ChannelProperty, value);
    }

    public ChipMode Mode
    {
        get => (ChipMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public static readonly RoutedEvent ChipClickedEvent = EventManager.RegisterRoutedEvent(
        nameof(ChipClicked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ChannelChip));

    public event RoutedEventHandler ChipClicked
    {
        add => AddHandler(ChipClickedEvent, value);
        remove => RemoveHandler(ChipClickedEvent, value);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        RaiseEvent(new RoutedEventArgs(ChipClickedEvent, this));
    }
}

public enum ChipMode { InputSignal, ActiveSpeaker }

