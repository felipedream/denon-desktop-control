// DENON Desktop Control
// Copyright (c) 2026 Felipe (@felipedream) - Buin, Santiago de Chile
// Licensed under MIT License
// https://github.com/felipedream/denon-desktop-control

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DenonRemote.Discovery;

/// <summary>
/// Network discovery for Denon / Marantz receivers.
///
/// Strategy:
///  1. Send an SSDP M-SEARCH bound to every physical IPv4 interface and
///     harvest the LOCATION header from responses whose manufacturer matches
///     one of the Sound-United brands (Denon, Marantz, D&amp;M).
///  2. If SSDP returns nothing (some routers / drivers block multicast, some
///     Windows environments have virtual interfaces that hijack the multicast
///     route), fall back to a parallel HTTP probe of every host in the
///     current /24 subnet. This is slow-ish (a few seconds) but always works.
/// </summary>
public sealed class SsdpDiscoveryService
{
    private const int SsdpPort = 1900;
    private static readonly IPAddress SsdpMulticast = IPAddress.Parse("239.255.255.250");
    private static readonly HttpClient Http = CreateHttp();

    private static readonly string[] SearchTargets =
    {
        "urn:schemas-denon-com:device:ACT-Denon:1",
        "urn:schemas-denon-com:device:AiosDevice:1",
        "urn:schemas-upnp-org:device:MediaRenderer:1",
        "ssdp:all"
    };

    public async Task<IReadOnlyList<ReceiverDescriptor>> DiscoverAsync(
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        var results = new ConcurrentDictionary<string, ReceiverDescriptor>(StringComparer.OrdinalIgnoreCase);

        // â”€â”€ 1) SSDP scan on every routable IPv4 interface â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var deadline = DateTime.UtcNow + timeout;
        var interfaces = GetIPv4Interfaces();
        var ssdpTasks = new List<Task>();
        foreach (var local in interfaces)
            foreach (var st in SearchTargets)
                ssdpTasks.Add(SsdpProbeAsync(local, st, results, deadline, ct));

        await Task.WhenAll(ssdpTasks).ConfigureAwait(false);

        // â”€â”€ 2) Fallback: parallel probe of the local /24 subnet â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        if (results.IsEmpty)
        {
            var subnetSw = Stopwatch.StartNew();
            await SubnetScanAsync(interfaces, results, ct).ConfigureAwait(false);
            Debug.WriteLine($"Subnet scan finished in {subnetSw.ElapsedMilliseconds} ms, found {results.Count}");
        }

        return results.Values
            .OrderBy(r => r.FriendlyName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task SsdpProbeAsync(
        IPAddress local,
        string st,
        ConcurrentDictionary<string, ReceiverDescriptor> results,
        DateTime deadline,
        CancellationToken ct)
    {
        UdpClient? udp = null;
        try
        {
            udp = new UdpClient(new IPEndPoint(local, 0));
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.MulticastLoopback = false;
            try { udp.JoinMulticastGroup(SsdpMulticast, local); } catch { /* interface may not support multicast */ }

            var payload = Encoding.ASCII.GetBytes(
                "M-SEARCH * HTTP/1.1\r\n" +
                "HOST: 239.255.255.250:1900\r\n" +
                "MAN: \"ssdp:discover\"\r\n" +
                "MX: 2\r\n" +
                $"ST: {st}\r\n\r\n");

            await udp.SendAsync(payload, payload.Length, new IPEndPoint(SsdpMulticast, SsdpPort))
                     .ConfigureAwait(false);

            while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero) break;

                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
                linked.CancelAfter(remaining);

                UdpReceiveResult reply;
                try { reply = await udp.ReceiveAsync(linked.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                catch (SocketException) { continue; }         // stray reset from a virtual iface
                catch { continue; }

                var text = Encoding.ASCII.GetString(reply.Buffer);
                var host = reply.RemoteEndPoint.Address.ToString();
                var loc = MatchHeader(text, "LOCATION");
                var server = MatchHeader(text, "SERVER") ?? "";

                if (loc is null) continue;
                // A cheap pre-filter avoids fetching XML for every random UPnP
                // device on the LAN. Real filtering happens after the HTTP GET.
                if (!LooksLikeDenon(server) && !LooksLikeDenon(loc)) continue;

                var descriptor = await FetchDescriptorAsync(host, loc, ct).ConfigureAwait(false);
                if (descriptor is not null) results.TryAdd(descriptor.Host, descriptor);
            }
        }
        catch { /* interface bind or send failed â€” try the others */ }
        finally
        {
            udp?.Dispose();
        }
    }

    private async Task SubnetScanAsync(
        IReadOnlyList<IPAddress> interfaces,
        ConcurrentDictionary<string, ReceiverDescriptor> results,
        CancellationToken ct)
    {
        // Enumerate every /24 subnet on our physical interfaces. This ignores
        // /24 assumption if the mask is smaller, but for home routers /24 is
        // the safe common case.
        var subnets = interfaces
            .Where(IsPrivate)
            .Select(BaseIp)
            .Distinct()
            .ToList();

        var probes = new List<Task>();
        foreach (var baseIp in subnets)
        {
            for (int i = 1; i < 255; i++)
            {
                var host = $"{baseIp}.{i}";
                probes.Add(ProbeHostAsync(host, results, ct));
            }
        }
        await Task.WhenAll(probes).ConfigureAwait(false);
    }

    private async Task ProbeHostAsync(
        string host,
        ConcurrentDictionary<string, ReceiverDescriptor> results,
        CancellationToken ct)
    {
        if (results.ContainsKey(host)) return;
        var descriptor = await ProbeAsync(host, ct).ConfigureAwait(false);
        if (descriptor is not null) results.TryAdd(host, descriptor);
    }

    /// <summary>
    /// Direct probe used when the user types an IP manually or the subnet
    /// scan runs. Tries the standard Denon endpoints on ports 8080 and 80.
    /// </summary>
    public async Task<ReceiverDescriptor?> ProbeAsync(string host, CancellationToken ct = default)
    {
        foreach (var port in new[] { 8080, 80 })
        {
            try
            {
                using var perRequestCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                // 2 s is plenty for a LAN reply. The Deviceinfo.xml payload on
                // modern AVRs is ~48 KB â€” 600 ms was too tight when a virtual
                // interface delayed the initial TCP handshake.
                perRequestCts.CancelAfter(TimeSpan.FromMilliseconds(2000));
                using var res = await Http.GetAsync(
                    $"http://{host}:{port}/goform/Deviceinfo.xml",
                    perRequestCts.Token).ConfigureAwait(false);
                if (!res.IsSuccessStatusCode) continue;
                var body = await res.Content.ReadAsStringAsync(perRequestCts.Token).ConfigureAwait(false);
                var doc = XDocument.Parse(body);
                var root = doc.Root;
                if (root is null) continue;

                var model = (string?)root.Element("ModelName")
                            ?? (string?)root.Element("ManualModelName") ?? "AVR";
                var brand = (string?)root.Element("BrandCode") switch
                {
                    "0" => "Denon",
                    "1" => "Marantz",
                    _ => "Denon"
                };
                return new ReceiverDescriptor(host, brand, model, model, "");
            }
            catch { /* try next port / next host */ }
        }
        return null;
    }

    private static async Task<ReceiverDescriptor?> FetchDescriptorAsync(string host, string location, CancellationToken ct)
    {
        try
        {
            using var response = await Http.GetAsync(location, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            var doc = XDocument.Parse(body);
            var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
            var device = doc.Descendants(ns + "device").FirstOrDefault();
            if (device is null) return null;

            var manufacturer = (string?)device.Element(ns + "manufacturer") ?? "";
            if (!IsSupportedBrand(manufacturer)) return null;

            var model = (string?)device.Element(ns + "modelName") ?? "";
            var friendly = (string?)device.Element(ns + "friendlyName") ?? "";
            var udn = (string?)device.Element(ns + "UDN") ?? "";
            return new ReceiverDescriptor(host, manufacturer, model, friendly, udn);
        }
        catch { return null; }
    }

    private static bool LooksLikeDenon(string s) =>
        s.Contains("denon", StringComparison.OrdinalIgnoreCase)
     || s.Contains("marantz", StringComparison.OrdinalIgnoreCase)
     || s.Contains("heos",    StringComparison.OrdinalIgnoreCase);

    private static bool IsSupportedBrand(string manufacturer)
    {
        if (string.IsNullOrWhiteSpace(manufacturer)) return false;
        return manufacturer.Contains("Denon", StringComparison.OrdinalIgnoreCase)
            || manufacturer.Contains("Marantz", StringComparison.OrdinalIgnoreCase)
            || manufacturer.Contains("D&M", StringComparison.OrdinalIgnoreCase)
            || manufacturer.Contains("Sound United", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<IPAddress> GetIPv4Interfaces()
    {
        var list = new List<IPAddress>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            var props = nic.GetIPProperties();
            foreach (var ua in props.UnicastAddresses)
            {
                if (ua.Address.AddressFamily == AddressFamily.InterNetwork)
                    list.Add(ua.Address);
            }
        }
        return list;
    }

    private static bool IsPrivate(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        return b[0] switch
        {
            10 => true,
            172 => b[1] >= 16 && b[1] <= 31,
            192 => b[1] == 168,
            _ => false
        };
    }

    private static string BaseIp(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        return $"{b[0]}.{b[1]}.{b[2]}";
    }

    private static string? MatchHeader(string text, string header)
    {
        var m = Regex.Match(text, "^" + Regex.Escape(header) + @":\s*(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static HttpClient CreateHttp()
    {
        var h = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(3)
        };
        h.DefaultRequestHeaders.UserAgent.ParseAdd("DenonRemote/1.0");
        return h;
    }
}

