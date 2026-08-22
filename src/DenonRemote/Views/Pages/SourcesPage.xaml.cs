// DENON Desktop Control
// Copyright (c) 2026 Felipe (@felipedream) - Buin, Santiago de Chile
// Licensed under MIT License
// https://github.com/felipedream/denon-desktop-control

using System.Windows;
using DenonRemote.ViewModels;

namespace DenonRemote.Views.Pages;

public partial class SourcesPage : PageBase
{
    public SourcesPage() => InitializeComponent();

    private void OnSourceClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is string token && DataContext is MainViewModel vm)
            _ = vm.SelectSourceCommand.ExecuteAsync(token);
    }

    private void OnSurroundClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is string mode && DataContext is MainViewModel vm)
            _ = vm.SetSurroundCommand.ExecuteAsync(mode);
    }
}
