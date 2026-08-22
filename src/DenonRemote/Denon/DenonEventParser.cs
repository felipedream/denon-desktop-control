// DENON Desktop Control
// Copyright (c) 2026 Felipe (@felipedream) - Buin, Santiago de Chile
// Licensed under MIT License
// https://github.com/felipedream/denon-desktop-control

using System;
using System.Linq;
using System.Windows;

namespace DenonRemote.Denon;

public sealed class DenonEventParser
{
    private readonly ReceiverState _state;

    public DenonEventParser(ReceiverState state) => _state = state;

    public void Feed(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        Application.Current?.Dispatcher.BeginInvoke(new Action(() => Apply(line)));
    }

    private void Apply(string line)
    {
        var s = line.TrimEnd();

        if (s == "PWON") { _state.Main.IsOn = true; return; }
        if (s == "PWSTANDBY") { _state.Main.IsOn = false; _state.Zone2.IsOn = false; return; }
        if (s == "ZMON") { _state.Main.IsOn = true; return; }
        if (s == "ZMOFF") { _state.Main.IsOn = false; return; }
        if (s == "Z2ON") { _state.Zone2.IsOn = true; return; }
        if (s == "Z2OFF") { _state.Zone2.IsOn = false; return; }

        if (s.StartsWith("MVMAX ", StringComparison.Ordinal))
        {
            if (int.TryParse(s.AsSpan(6).Trim(), out var max))
                _state.Main.VolumeMax = max;
            return;
        }
        if (s.StartsWith("MV", StringComparison.Ordinal) && !s.StartsWith("MVMAX"))
        {
            var payload = s[2..];
            var mv = DenonProtocol.ParseVolume(payload);
            if (mv is not null) _state.Main.Volume = mv.Value;
            return;
        }

        if (s.StartsWith("Z2", StringComparison.Ordinal))
        {
            var rest = s[2..];
            if (rest == "ON") { _state.Zone2.IsOn = true; return; }
            if (rest == "OFF") { _state.Zone2.IsOn = false; return; }
            if (rest == "MUON") { _state.Zone2.IsMuted = true; return; }
            if (rest == "MUOFF") { _state.Zone2.IsMuted = false; return; }
            if (rest.Length is >= 2 and <= 3 && int.TryParse(rest, out _))
            {
                var v = DenonProtocol.ParseVolume(rest);
                if (v is not null) _state.Zone2.Volume = v.Value;
                return;
            }
            if (!rest.StartsWith("SLP", StringComparison.Ordinal))
            {
                _state.Zone2.Source = rest;
                _state.Zone2.SourceFriendly = rest;
            }
            return;
        }

        if (s == "MUON") { _state.Main.IsMuted = true; return; }
        if (s == "MUOFF") { _state.Main.IsMuted = false; return; }

        if (s.StartsWith("SI", StringComparison.Ordinal))
        {
            var src = s[2..];
            _state.Main.Source = src;
            _state.Main.SourceFriendly = LookupSourceFriendly(src);
            return;
        }

        if (s.StartsWith("MS", StringComparison.Ordinal) && !s.StartsWith("MSSMART") && !s.StartsWith("MSQUICK"))
        {
            _state.SurroundMode = s[2..].Trim();
            return;
        }

        if (s.StartsWith("PSBAS ", StringComparison.Ordinal))
        {
            if (int.TryParse(s.AsSpan(6).Trim(), out var v)) _state.Bass = v - 50;
            return;
        }
        if (s.StartsWith("PSTRE ", StringComparison.Ordinal))
        {
            if (int.TryParse(s.AsSpan(6).Trim(), out var v)) _state.Treble = v - 50;
            return;
        }
        if (s.StartsWith("PSSWL ", StringComparison.Ordinal))
        {
            if (int.TryParse(s.AsSpan(6).Trim(), out var v)) _state.SubwooferLevel = v - 50;
            return;
        }
        if (s.StartsWith("PSTONE CTRL ", StringComparison.Ordinal))
        {
            _state.ToneControlEnabled = s.EndsWith("ON", StringComparison.Ordinal);
            return;
        }

        if (s.StartsWith("ECO", StringComparison.Ordinal))
        {
            _state.EcoMode = s[3..].Trim() switch
            {
                "AUTO" => "Auto", "ON" => "On", "OFF" => "Off", _ => _state.EcoMode
            };
            return;
        }

        if (s.StartsWith("NSFRN ", StringComparison.Ordinal))
        {
            _state.FriendlyName = s[6..].Trim();
            return;
        }

        if (s.StartsWith("SYSDA", StringComparison.Ordinal))
        {
            _state.AudioFormat = DenonProtocol.PrettifyAudioFormat(s[5..]);
            return;
        }
        if (s.StartsWith("SSINFAISFSV ", StringComparison.Ordinal))
        {
            _state.SampleRate = s[12..].Trim();
            return;
        }

        if (s.StartsWith("OPINFINS ", StringComparison.Ordinal))
        {
            ApplyMatrix(s[9..].Trim(), input: true);
            return;
        }
        if (s.StartsWith("OPINFASP ", StringComparison.Ordinal))
        {
            ApplyMatrix(s[9..].Trim(), input: false);
            return;
        }

        // Channel level trim
        if (s.StartsWith("CV", StringComparison.Ordinal) && s.Length > 2 && s != "CVEND")
        {
            var spaceIdx = s.IndexOf(' ');
            if (spaceIdx > 2 && spaceIdx < s.Length - 1)
            {
                var channel = s.Substring(2, spaceIdx - 2);
                var raw = s[(spaceIdx + 1)..].Trim();
                var parsed = DenonProtocol.ParseVolume(raw);
                if (parsed is not null)
                {
                    var target = _state.Channel(channel);
                    if (target is not null)
                        target.LevelDb = parsed.Value - 50;
                }
            }
            return;
        }

        // Source renames: SSFUN<token> <user label>
        if (s.StartsWith("SSFUN", StringComparison.Ordinal))
        {
            if (s == "SSFUN END") return;
            var payload = s[5..];
            string token = "", label = "";
            foreach (var kt in new[] { "SAT/CBL", "MPLAY", "GAME1", "GAME2", "AUX1", "AUX2", "BD", "TV", "CD", "PHONO", "TUNER", "NET", "BT", "IRADIO" })
            {
                if (payload.StartsWith(kt, StringComparison.OrdinalIgnoreCase))
                { token = kt; label = payload[kt.Length..].Trim(); break; }
            }
            if (string.IsNullOrEmpty(token)) return;
            var existing = _state.Sources.FirstOrDefault(x => string.Equals(x.Token, token, StringComparison.OrdinalIgnoreCase));
            if (existing is not null) existing.Rename = label;
            else _state.Sources.Add(new SourceEntry { Token = token, Display = token, Rename = label });
            return;
        }

        // Source visibility: SSSOD<token> USE|DEL
        if (s.StartsWith("SSSOD", StringComparison.Ordinal))
        {
            if (s == "SSSOD END") return;
            var payload = s[5..];
            foreach (var kt in new[] { "SAT/CBL", "MPLAY", "GAME1", "GAME2", "AUX1", "AUX2", "BD", "TV", "CD", "PHONO", "TUNER", "NET", "BT", "IRADIO" })
            {
                if (payload.StartsWith(kt, StringComparison.OrdinalIgnoreCase))
                {
                    var rest = payload[kt.Length..].Trim();
                    if (rest == "DEL")
                    {
                        var toRemove = _state.Sources.FirstOrDefault(x => string.Equals(x.Token, kt, StringComparison.OrdinalIgnoreCase));
                        if (toRemove is not null) _state.Sources.Remove(toRemove);
                    }
                    else if (rest == "USE")
                    {
                        if (!_state.Sources.Any(x => string.Equals(x.Token, kt, StringComparison.OrdinalIgnoreCase)))
                            _state.Sources.Add(new SourceEntry { Token = kt, Display = kt, Rename = "" });
                    }
                    break;
                }
            }
            return;
        }
    }

    private void ApplyMatrix(string payload, bool input)
    {
        var channels = _state.Channels;
        for (int i = 0; i < payload.Length && i < channels.Count; i++)
        {
            var active = payload[i] == '2';
            if (input) channels[i].IsInputActive = active;
            else channels[i].IsSpeakerActive = active;
        }
    }

    private string LookupSourceFriendly(string token)
    {
        var src = _state.Sources.FirstOrDefault(x => string.Equals(x.Token, token, StringComparison.OrdinalIgnoreCase));
        if (src is not null) return src.Caption;
        if (DenonProtocol.DefaultSources.TryGetValue(token, out var friendly)) return friendly;
        return token;
    }
}
