// DENON Desktop Control
// Copyright (c) 2026 Felipe (@felipedream) - Buin, Santiago de Chile
// Licensed under MIT License
// https://github.com/felipedream/denon-desktop-control

using System;
using System.Windows;
using System.Windows.Input;
using DenonRemote.Services;
using DenonRemote.ViewModels;
using Wpf.Ui.Controls;

namespace DenonRemote.Views;

public partial class WidgetWindow : FluentWindow
{
    public event EventHandler? OpenFullRequested;

    public WidgetWindow(MainViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }

    /// <summary>
    /// Positions the widget above the system tray (bottom-right of the primary screen).
    /// </summary>
    public void ShowNearTray()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 12;
        Top = workArea.Bottom - Height - 12;
        Show();
        Activate();
    }

    public void Toggle()
    {
        if (IsVisible) Hide();
        else ShowNearTray();
    }

    private void OnSourceClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is string tag && DataContext is MainViewModel vm)
            _ = vm.SelectSourceCommand.ExecuteAsync(tag);
    }

    private void OnOpenFull(object sender, MouseButtonEventArgs e)
    {
        Hide();
        OpenFullRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        // Auto-hide when losing focus (click outside)
        Hide();
    }
}

