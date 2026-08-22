// DENON Desktop Control
// Copyright (c) 2026 Felipe (@felipedream) - Buin, Santiago de Chile
// Licensed under MIT License
// https://github.com/felipedream/denon-desktop-control

using System.Windows;
using DenonRemote.Services;

namespace DenonRemote.Views.Pages;

public partial class ConnectPage : PageBase
{
    public ConnectPage()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        if (FindName("HeroTitle") is System.Windows.Controls.TextBlock t1) t1.Text = L.AddReceiver;
        if (FindName("HeroSubtitle") is System.Windows.Controls.TextBlock t2) t2.Text = L.AddReceiverDesc;
        if (FindName("DiscTitle") is System.Windows.Controls.TextBlock t3) t3.Text = L.DiscoveredDevices;
        if (FindName("IpTitle") is System.Windows.Controls.TextBlock t4) t4.Text = L.AddByIp;
        if (FindName("IpDesc") is System.Windows.Controls.TextBlock t5) t5.Text = L.AddByIpDesc;
        if (FindName("HelpTitle") is System.Windows.Controls.TextBlock t6) t6.Text = L.DoesntShowUp;
        if (FindName("HelpDesc") is System.Windows.Controls.TextBlock t7) t7.Text = L.DoesntShowUpDesc;
    }
}
