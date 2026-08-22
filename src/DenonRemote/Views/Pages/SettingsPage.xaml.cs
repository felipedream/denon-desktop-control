// DENON Desktop Control
// Copyright (c) 2026 Felipe (@felipedream) - Buin, Santiago de Chile
// Licensed under MIT License
// https://github.com/felipedream/denon-desktop-control

using System.Diagnostics;
using System.Windows;
using DenonRemote.Services;
using DenonRemote.ViewModels;

namespace DenonRemote.Views.Pages;

public partial class SettingsPage : PageBase
{
    public SettingsPage()
    {
        InitializeComponent();
        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        TitleText.Text = L.SettingsTitle;
        AutoConnLabel.Text = L.SettingsAutoConnect;
        AutoConnDesc.Text = L.SettingsAutoConnectDesc;
        TrayLabel.Text = L.SettingsCloseToTray;
        TrayDesc.Text = L.SettingsCloseToTrayDesc;
        MinLabel.Text = L.SettingsStartMinimized;
        MinDesc.Text = L.SettingsStartMinimizedDesc;
        UpdateLabel.Text = L.SettingsAutoUpdate;
        UpdateDesc.Text = L.SettingsAutoUpdateDesc;
        FreeVersionText.Text = L.AboutFreeVersion;
        CreatedByLabel.Text = L.AboutCreatedBy.ToUpperInvariant();
        DonateBtn.Content = L.AboutDonate;
        TelegramBtn.Content = L.AboutTelegram;
        VersionRun.Text = "v" + (DataContext is MainViewModel vm ? vm.Updater.CurrentVersion : "1.0.0");
        UpdateAvailRun.Text = L.IsSpanish ? "Nueva versión: " : "New version: ";
    }

    private void OnTelegramClick(object sender, RoutedEventArgs e) =>
        OpenUrl("https://t.me/felipedream");

    private void OnDonateClick(object sender, RoutedEventArgs e) =>
        OpenUrl("https://www.paypal.com/donate/?hosted_button_id=&business=felipedream@gmail.com&currency_code=USD");

    private void OnUpdateClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.AvailableUpdate is not null)
            vm.Updater.ApplyUpdate(vm.AvailableUpdate);
    }

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* ignored */ }
    }
}

