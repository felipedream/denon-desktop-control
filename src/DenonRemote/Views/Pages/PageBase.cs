// DENON Desktop Control
// Copyright (c) 2026 Felipe (@felipedream) - Buin, Santiago de Chile
// Licensed under MIT License
// https://github.com/felipedream/denon-desktop-control

using System.Windows;
using System.Windows.Controls;

namespace DenonRemote.Views.Pages;

/// <summary>
/// Base class for every navigation page. Ensures the DataContext is
/// inherited from the parent window at load time. RelativeSource bindings on
/// the UserControl root do not work reliably inside WPF-UI's NavigationView,
/// so we set it imperatively here.
/// </summary>
public abstract class PageBase : UserControl
{
    protected PageBase()
    {
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is null || DataContext == this)
            DataContext = Window.GetWindow(this)?.DataContext;
    }
}

