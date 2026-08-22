// DENON Desktop Control
// Copyright (c) 2026 Felipe (@felipedream) - Buin, Santiago de Chile
// Licensed under MIT License
// https://github.com/felipedream/denon-desktop-control

using System.ComponentModel;
using System.Windows;
using DenonRemote.Services;
using DenonRemote.ViewModels;
using Wpf.Ui.Controls;

namespace DenonRemote.Views;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _vm;
    private readonly AppSettings _settings;
    private WidgetWindow? _widget;

    public MainWindow(MainViewModel vm, AppSettings settings)
    {
        _vm = vm;
        _settings = settings;
        DataContext = vm;
        InitializeComponent();

        // Localize navigation labels
        NavHome.Content = L.Dashboard;
        NavSources.Content = L.Sources;
        NavSound.Content = L.Sound;
        NavZones.Content = L.Zones;
        NavDevices.Content = L.Devices;
        NavSettings.Content = L.Settings;
        RecentLabel.Text = L.Recent;

        if (settings.StartMinimized) WindowState = WindowState.Minimized;
    }

    private WidgetWindow GetOrCreateWidget()
    {
        if (_widget is null || !_widget.IsLoaded)
        {
            _widget = new WidgetWindow(_vm);
            _widget.OpenFullRequested += (_, _) => ToggleWindow(force: true);
        }
        return _widget;
    }

    /// <summary>
    /// Triggered when the NavigationView finishes loading. Navigates to the
    /// first page (Dashboard/Inicio) so the content area is never empty.
    /// </summary>
    private void OnNavLoaded(object sender, RoutedEventArgs e)
    {
        // Select the first item = Dashboard
        if (Nav.MenuItems.Count > 0)
            Nav.Navigate(typeof(Views.Pages.DashboardPage));
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_settings.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }

    private void OnTrayDoubleClick(object sender, RoutedEventArgs e) => ToggleWindow(force: true);

    private void OnTrayLeftClick(object sender, RoutedEventArgs e) => GetOrCreateWidget().Toggle();

    private void OnShowWindow(object sender, RoutedEventArgs e) => ToggleWindow(force: true);

    private void OnQuit(object sender, RoutedEventArgs e)
    {
        _settings.CloseToTray = false;
        Application.Current.Shutdown();
    }

    private void ToggleWindow(bool force = false)
    {
        if (force || !IsVisible)
        {
            Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
            Topmost = true;
            Topmost = false;
        }
        else
        {
            Hide();
        }
    }
}

