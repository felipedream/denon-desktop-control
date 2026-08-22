// DENON Desktop Control
// Copyright (c) 2026 Felipe (@felipedream) - Buin, Santiago de Chile
// Licensed under MIT License
// https://github.com/felipedream/denon-desktop-control

using System;
using System.Windows;
using System.Windows.Threading;
using DenonRemote.Controls;
using DenonRemote.Denon;
using DenonRemote.Services;
using DenonRemote.ViewModels;

namespace DenonRemote.Views.Pages;

public partial class DashboardPage : PageBase
{
    // Debounces the CV command so a slider drag doesn't flood the AVR.
    private readonly DispatcherTimer _debounce;
    private ChannelState? _pendingChannel;
    private double _pendingValue;
    private bool _suppressChange;    // silences ValueChanged while we set slider from state

    private static readonly string[] SurroundModes = {
        "STEREO", "DIRECT", "PURE DIRECT", "AUTO",
        "MCH STEREO", "M CH IN", "M CH IN+DS", "M CH IN+NEURAL:X", "M CH IN+VIRTUAL",
        "MOVIE", "MUSIC", "GAME", "VIDEO GAME",
        "ROCK ARENA", "JAZZ CLUB", "MATRIX", "MONO MOVIE",
        "DOLBY SURROUND", "NEURAL:X"
    };

    public DashboardPage()
    {
        InitializeComponent();
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _debounce.Tick += OnDebounceTick;
        Loaded += (_, _) => PopulateSurroundList();
    }

    private void PopulateSurroundList()
    {
        SurroundList.Children.Clear();
        foreach (var mode in SurroundModes)
        {
            var btn = new System.Windows.Controls.Button
            {
                Content = mode,
                Tag = mode,
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 2, 0, 2),
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left,
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btn.Click += OnSurroundPopupClick;
            SurroundList.Children.Add(btn);
        }
    }

    private void OnSourceCardClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        SourcePopup.IsOpen = true;
    }

    private void OnSurroundCardClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        SurroundPopup.IsOpen = true;
    }

    private void OnSourcePopupClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is string token && DataContext is MainViewModel vm)
        {
            _ = vm.SelectSourceCommand.ExecuteAsync(token);
            SourcePopup.IsOpen = false;
        }
    }

    private void OnSurroundPopupClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is string mode && DataContext is MainViewModel vm)
        {
            _ = vm.SetSurroundCommand.ExecuteAsync(mode);
            SurroundPopup.IsOpen = false;
        }
    }

    private void OnSpeakerClicked(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not ChannelChip chip || chip.Channel is null) return;
        if (!chip.Channel.IsSpeakerActive) return;
        if (!DenonProtocol.ChannelSupportsTrim(chip.Channel.Label)) return;

        _pendingChannel = chip.Channel;
        _suppressChange = true;
        TrimTitle.Text = chip.Channel.Label;
        TrimSlider.Value = chip.Channel.LevelDb;
        UpdateTrimValueLabel(chip.Channel.LevelDb);
        _suppressChange = false;
        TrimPopup.IsOpen = true;
    }

    private void OnTrimChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressChange || _pendingChannel is null) return;
        _pendingValue = e.NewValue;
        UpdateTrimValueLabel(e.NewValue);
        _debounce.Stop();
        _debounce.Start();
    }

    private void OnDebounceTick(object? sender, EventArgs e)
    {
        _debounce.Stop();
        if (_pendingChannel is null) return;
        if (DataContext is not MainViewModel vm) return;
        _ = vm.SetChannelTrimAsync(_pendingChannel.Label, _pendingValue);
    }

    private void UpdateTrimValueLabel(double db)
    {
        TrimValue.Text = db switch
        {
            > 0 => $"+{db:0.#} dB",
            < 0 => $"{db:0.#} dB",
            _   => "0 dB"
        };
    }
}

