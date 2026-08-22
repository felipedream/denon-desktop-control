// DENON Desktop Control
// Copyright (c) 2026 Felipe (@felipedream) - Buin, Santiago de Chile
// Licensed under MIT License
// https://github.com/felipedream/denon-desktop-control

using System.Collections.Generic;

namespace DenonRemote.Denon;

/// <summary>
/// Static maps used to interpret Denon/Marantz AVR telnet responses.
/// Reference: Denon AVR Control Protocol (undocumented public spec used by
/// third-party integrations such as denonavr and heos-cli).
/// </summary>
public static class DenonProtocol
{
    /// <summary>
    /// Channel layout used by <c>OPINFINS</c> and <c>OPINFASP</c> strings.
    /// Each character is a digit representing the state of one channel
    /// (0 = not present / 1 = present but inactive / 2 = active).
    /// The order below matches the way modern Denon receivers (Gen 2, X-series,
    /// S-series 2020+) publish those strings.
    /// </summary>
    public static readonly string[] SignalChannels =
    {
        "FL",  "FR",  "C",   "SW",
        "SL",  "SR",  "SBL", "SBR",
        "SB",  "FHL", "FHR", "TFL",
        "TFR", "TML", "TMR", "TRL",
        "TRR", "RRL", "RRR", "FWL",
        "FWR", "LFE", "EXT"
    };

    /// <summary>
    /// Channels the AVR exposes a <c>CV&lt;label&gt;</c> trim for. LFE and EXT
    /// don't have their own per-channel level in this protocol.
    /// </summary>
    public static bool ChannelSupportsTrim(string label) => label switch
    {
        "FL" or "FR" or "C" or "SW"
        or "SL" or "SR" or "SBL" or "SBR" or "SB"
        or "FHL" or "FHR"
        or "TFL" or "TFR" or "TML" or "TMR" or "TRL" or "TRR"
        or "RRL" or "RRR" or "FWL" or "FWR" => true,
        _ => false
    };

    /// <summary>
    /// Human readable audio format detected on the current source
    /// (payload of the <c>SYSDA</c> telnet event).
    /// </summary>
    public static string PrettifyAudioFormat(string raw)
    {
        var v = (raw ?? string.Empty).Trim();
        return v switch
        {
            "PCM" => "PCM",
            "DOLBY DIGITAL" => "Dolby Digital",
            "DOLBY DIGITAL PLUS" => "Dolby Digital+",
            "DOLBY TRUEHD" => "Dolby TrueHD",
            "DTS" => "DTS",
            "DTS-HD MA" => "DTS-HD Master Audio",
            "DTS-HD HR" => "DTS-HD HR",
            _ => string.IsNullOrWhiteSpace(v) ? "-" : v
        };
    }

    /// <summary>
    /// Convert a Denon volume token (MV50, MV505, MV0) into a floating value
    /// on the 0..MVMAX display scale where 0.5 dB steps are supported.
    /// </summary>
    public static double? ParseVolume(string mv)
    {
        if (string.IsNullOrWhiteSpace(mv)) return null;
        var digits = mv.Trim();
        if (digits.Length == 3) // half step: 505 â†’ 50.5
        {
            if (int.TryParse(digits[..2], out var whole) &&
                int.TryParse(digits[2..], out var frac))
                return whole + frac / 10.0;
        }
        if (int.TryParse(digits, out var i))
            return i;
        return null;
    }

    /// <summary>
    /// Encode a display volume (0..99, half steps allowed) into the token used
    /// by the AVR telnet command <c>MV</c>.
    /// </summary>
    public static string FormatVolume(double value)
    {
        value = Clamp(value, 0, 99);
        var rounded = System.Math.Round(value * 2, System.MidpointRounding.AwayFromZero) / 2.0;
        var whole = (int)System.Math.Floor(rounded);
        var half = rounded - whole >= 0.25;
        return half
            ? $"{whole:00}5"
            : $"{whole:00}";
    }

    /// <summary>
    /// Common Denon input sources with a friendly caption suitable for the UI.
    /// The internal key is what the AVR uses in the <c>SI</c> command.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> DefaultSources = new Dictionary<string, string>
    {
        ["CBL/SAT"] = "Cable / SAT",
        ["MPLAY"] = "Media Player",
        ["BD"] = "Blu-ray",
        ["GAME"] = "Game",
        ["AUX1"] = "AUX 1",
        ["AUX2"] = "AUX 2",
        ["TV"] = "TV Audio",
        ["CD"] = "CD",
        ["PHONO"] = "Phono",
        ["TUNER"] = "Tuner",
        ["NET"] = "HEOS Music",
        ["BT"] = "Bluetooth",
        ["IRADIO"] = "Internet Radio",
        ["SPOTIFY"] = "Spotify",
        ["TIDAL"] = "Tidal",
        ["AMAZON"] = "Amazon Music",
        ["DEEZER"] = "Deezer",
        ["USB/IPOD"] = "USB / iPod"
    };

    /// <summary>Denon surround modes (payload of MS command).</summary>
    public static readonly IReadOnlyDictionary<string, string> SurroundModes = new Dictionary<string, string>
    {
        ["MOVIE"] = "Movie",
        ["MUSIC"] = "Music",
        ["GAME"] = "Game",
        ["DIRECT"] = "Direct",
        ["PURE DIRECT"] = "Pure Direct",
        ["STEREO"] = "Stereo",
        ["AUTO"] = "Auto",
        ["DOLBY DIGITAL"] = "Dolby",
        ["DTS SURROUND"] = "DTS",
        ["MCH STEREO"] = "Multi Ch Stereo",
        ["ROCK ARENA"] = "Rock Arena",
        ["JAZZ CLUB"] = "Jazz Club",
        ["MONO MOVIE"] = "Mono Movie",
        ["MATRIX"] = "Matrix",
        ["VIDEO GAME"] = "Video Game",
        ["VIRTUAL"] = "Virtual"
    };

    private static double Clamp(double v, double min, double max) =>
        v < min ? min : v > max ? max : v;
}

