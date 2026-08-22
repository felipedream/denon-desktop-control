// DENON Desktop Control
// Copyright (c) 2026 Felipe (@felipedream) - Buin, Santiago de Chile
// Licensed under MIT License
// https://github.com/felipedream/denon-desktop-control

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DenonRemote.Denon;

/// <summary>
/// HTTP fallback for the AVR "goform" API. Used when the persistent telnet
/// connection is not available â€” most importantly to power the receiver on
/// from standby, since port 23 is closed while the unit sleeps but the goform
/// endpoints (port 8080 on modern models, port 80 on older ones) keep
/// answering as long as Network Standby is enabled.
/// </summary>
public sealed class DenonHttpClient
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(4)
    };

    private readonly string _host;
    private int _preferredPort = 8080; // start with the modern AVR port

    public DenonHttpClient(string host) => _host = host;

    /// <summary>
    /// Sends a direct AVR command (same tokens as the telnet CLI):
    /// PWON, PWSTANDBY, MUON, MUOFF, MVUP, MVDOWN, SICD, MSSTEREO, ...
    /// </summary>
    public async Task<bool> SendDirectAsync(string command, CancellationToken ct = default)
    {
        var path = $"/goform/formiPhoneAppDirect.xml?{command}";
        return await TryOnBothPortsAsync(path, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the compact main-zone status (power / input / volume / mute).
    /// Used to hydrate the state on startup when telnet is temporarily gone.
    /// </summary>
    public async Task<(bool Power, string Input, double Volume, bool Mute)?>
        GetMainStatusAsync(CancellationToken ct = default)
    {
        var body = await GetAsync("/goform/formMainZone_MainZoneXmlStatusLite.xml", ct).ConfigureAwait(false);
        if (body is null) return null;
        try
        {
            var doc = XDocument.Parse(body);
            var item = doc.Root;
            if (item is null) return null;
            var power = string.Equals((string?)item.Element("Power")?.Element("value"), "ON", StringComparison.OrdinalIgnoreCase);
            var input = (string?)item.Element("InputFuncSelect")?.Element("value") ?? "";
            var volS = (string?)item.Element("MasterVolume")?.Element("value") ?? "0";
            double.TryParse(volS, System.Globalization.NumberStyles.Any,
                             System.Globalization.CultureInfo.InvariantCulture, out var absDb);
            // Absolute (-80..+18 dB) â†’ relative (0..99) where 0 dB = 80.
            var relative = absDb + 80.0;
            var mute = string.Equals((string?)item.Element("Mute")?.Element("value"), "on", StringComparison.OrdinalIgnoreCase);
            return (power, input, relative, mute);
        }
        catch { return null; }
    }

    private async Task<bool> TryOnBothPortsAsync(string path, CancellationToken ct)
    {
        foreach (var port in new[] { _preferredPort, _preferredPort == 8080 ? 80 : 8080 })
        {
            try
            {
                using var res = await Http.GetAsync($"http://{_host}:{port}{path}", ct).ConfigureAwait(false);
                if (res.IsSuccessStatusCode)
                {
                    _preferredPort = port; // remember what worked
                    return true;
                }
            }
            catch { /* try next */ }
        }
        return false;
    }

    private async Task<string?> GetAsync(string path, CancellationToken ct)
    {
        foreach (var port in new[] { _preferredPort, _preferredPort == 8080 ? 80 : 8080 })
        {
            try
            {
                using var res = await Http.GetAsync($"http://{_host}:{port}{path}", ct).ConfigureAwait(false);
                if (!res.IsSuccessStatusCode) continue;
                _preferredPort = port;
                return await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            }
            catch { /* try next */ }
        }
        return null;
    }
}

