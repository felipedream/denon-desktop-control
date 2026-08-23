// DENON Desktop Control
// Copyright (c) 2026 Felipe (@felipedream) - Buin, Santiago de Chile
// Licensed under MIT License
// https://github.com/felipedream/denon-desktop-control

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DenonRemote.Services;
using DenonRemote.ViewModels;

namespace DenonRemote.Views.Pages;

public partial class SoundPage : PageBase
{
    // One debounce timer per channel so dragging several sliders in a row
    // doesn't cancel each other's pending command.
    private readonly Dictionary<string, DispatcherTimer> _debouncers = new();

    public SoundPage()
    {
        InitializeComponent();
        // The speaker matrix may already be populated by the time the page is
        // shown, and PropertyChanged only fires on transitions — so rebuild the
        // list explicitly whenever the page becomes visible.
        Loaded += (_, _) =>
        {
            if (DataContext is MainViewModel vm) vm.RefreshActiveChannels();
        };
    }

    private void OnChannelLevelChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is not Slider slider || slider.Tag is not string channel) return;
        if (DataContext is not MainViewModel vm) return;

        if (!_debouncers.TryGetValue(channel, out var timer))
        {
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _debouncers[channel] = timer;
        }

        timer.Stop();
        var value = e.NewValue;
        timer.Tick -= OnTick;
        timer.Tick += OnTick;
        timer.Start();

        void OnTick(object? s, EventArgs args)
        {
            timer.Stop();
            timer.Tick -= OnTick;
            _ = vm.SetChannelLevelAsync(channel, value);
        }
    }

    // ── Profile chips ───────────────────────────────────────────────────────

    private void OnLoadProfile(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement el || el.Tag is not ChannelProfile p) return;
        if (DataContext is not MainViewModel vm) return;
        _ = vm.LoadProfileCommand.ExecuteAsync(p);
    }

    private void OnDeleteProfile(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement el || el.Tag is not ChannelProfile p) return;
        if (DataContext is not MainViewModel vm) return;
        vm.DeleteProfileCommand.Execute(p);
        e.Handled = true;
    }

    /// <summary>Double-click on a chip swaps the label for an inline text box.</summary>
    private void OnRenameProfile(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Button btn) return;
        var (text, edit) = FindChipParts(btn);
        if (text is null || edit is null) return;

        text.Visibility = Visibility.Collapsed;
        edit.Visibility = Visibility.Visible;
        edit.Focus();
        edit.SelectAll();
        e.Handled = true;
    }

    private void OnRenameKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Escape)) return;
        if (sender is TextBox tb) CommitRename(tb);
        e.Handled = true;
    }

    private void OnRenameCommit(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb) CommitRename(tb);
    }

    private void CommitRename(TextBox edit)
    {
        // Hide the editor and persist. The binding already pushed the new name
        // into the profile object, so we just need to save the settings file.
        var parent = VisualTreeHelper.GetParent(edit) as Grid;
        if (parent is not null)
        {
            foreach (var child in parent.Children)
                if (child is TextBlock tbk) tbk.Visibility = Visibility.Visible;
        }
        edit.Visibility = Visibility.Collapsed;

        if (DataContext is MainViewModel vm) vm.PersistProfiles();
    }

    private static (TextBlock?, TextBox?) FindChipParts(Button btn)
    {
        if (btn.Content is not Grid g) return (null, null);
        TextBlock? text = null;
        TextBox? edit = null;
        foreach (var child in g.Children)
        {
            if (child is TextBlock t) text = t;
            else if (child is TextBox b) edit = b;
        }
        return (text, edit);
    }
}
