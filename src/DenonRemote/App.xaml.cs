// DENON Desktop Control
// Copyright (c) 2026 Felipe (@felipedream) - Buin, Santiago de Chile
// Licensed under MIT License
// https://github.com/felipedream/denon-desktop-control

using System;
using System.Windows;
using DenonRemote.Discovery;
using DenonRemote.Services;
using DenonRemote.ViewModels;
using DenonRemote.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DenonRemote;

public partial class App : Application
{
    private IServiceProvider? _services;

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        // WPF-UI defaults to the Windows accent color. Override it with the
        // Denon brand red so every "Appearance=Primary" button, toggle track
        // and slider thumb picks it up automatically.
        Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(
            System.Windows.Media.Color.FromRgb(0xE6, 0x39, 0x46),
            Wpf.Ui.Appearance.ApplicationTheme.Dark);

        // Global exception handler â€” writes to %TEMP%\DenonRemote-crash.log
        DispatcherUnhandledException += (_, ev) =>
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DenonRemote-crash.log");
            System.IO.File.WriteAllText(path, ev.Exception.ToString());
            MessageBox.Show(ev.Exception.ToString(), "DenonRemote crashed");
            ev.Handled = true;
            Shutdown();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ev) =>
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DenonRemote-crash.log");
            System.IO.File.WriteAllText(path, ev.ExceptionObject?.ToString() ?? "unknown");
        };

        var services = new ServiceCollection();
        services.AddSingleton(AppSettings.Load());
        services.AddSingleton<SsdpDiscoveryService>();
        services.AddSingleton<ReceiverService>();
        services.AddSingleton<AutoUpdateService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        _services = services.BuildServiceProvider();

        var vm = _services.GetRequiredService<MainViewModel>();
        var window = _services.GetRequiredService<MainWindow>();
        window.Show();

        // Runs asynchronously â€” the UI is already alive by the time this awaits.
        await vm.InitializeAsync();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_services is IAsyncDisposable disposable)
            await disposable.DisposeAsync();
        base.OnExit(e);
    }
}

