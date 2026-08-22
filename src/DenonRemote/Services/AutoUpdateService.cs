// DENON Desktop Control
// Copyright (c) 2026 Felipe (@felipedream) - Buin, Santiago de Chile
// Licensed under MIT License
// https://github.com/felipedream/denon-desktop-control

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace DenonRemote.Services;

/// <summary>
/// Lightweight auto-update. On startup, the app hits a small JSON manifest at
/// <c>https://haussmed.cl/denon/update.json</c>. If there is a newer version
/// it downloads the ZIP and prompts the user to restart.
///
/// Manifest format expected:
/// <code>
/// {
///   "version": "1.0.1",
///   "url": "https://haussmed.cl/denon/DenonRemote-1.0.1.zip",
///   "notes": "Bug fixes and improved discovery."
/// }
/// </code>
///
/// The app's current version is read from Assembly.
/// </summary>
public sealed class AutoUpdateService
{
    private const string ManifestUrl = "https://haussmed.cl/denon/update.json";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public string CurrentVersion { get; }

    public AutoUpdateService()
    {
        var asm = Assembly.GetExecutingAssembly();
        CurrentVersion = asm.GetName().Version?.ToString(3) ?? "1.0.0";
    }

    /// <summary>
    /// Checks the remote manifest. Returns the download URL if a newer
    /// version is available, null otherwise.
    /// </summary>
    public async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            var json = await Http.GetStringAsync(ManifestUrl).ConfigureAwait(false);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var remoteVersion = root.GetProperty("version").GetString() ?? "0.0.0";
            var url = root.GetProperty("url").GetString() ?? "";
            var notes = root.TryGetProperty("notes", out var n) ? n.GetString() ?? "" : "";

            if (IsNewer(remoteVersion, CurrentVersion))
                return new UpdateInfo(remoteVersion, url, notes);
            return null;
        }
        catch
        {
            return null; // silent on error
        }
    }

    /// <summary>
    /// Downloads the zip, extracts next to the current exe, and launches
    /// a helper script that swaps folders and restarts the app.
    /// For now, just opens the download URL in the browser.
    /// </summary>
    public void ApplyUpdate(UpdateInfo info)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = info.Url,
                UseShellExecute = true
            });
        }
        catch { /* best effort */ }
    }

    private static bool IsNewer(string remote, string local)
    {
        if (Version.TryParse(remote, out var r) && Version.TryParse(local, out var l))
            return r > l;
        return false;
    }
}

public sealed record UpdateInfo(string Version, string Url, string Notes);

