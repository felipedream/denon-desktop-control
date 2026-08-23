// DENON Desktop Control
// Copyright (c) 2026 Felipe (@felipedream) - Buin, Santiago de Chile
// Licensed under MIT License
// https://github.com/felipedream/denon-desktop-control

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace DenonRemote.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type _, object? __, CultureInfo ___) =>
        (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type _, object? __, CultureInfo ___) => throw new NotSupportedException();
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type _, object? __, CultureInfo ___) =>
        (value is bool b && b) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type _, object? __, CultureInfo ___) => throw new NotSupportedException();
}

public sealed class BoolToColorConverter : IValueConverter
{
    public Brush? TrueBrush { get; set; }
    public Brush? FalseBrush { get; set; }
    public object Convert(object value, Type _, object? __, CultureInfo ___) =>
        (value is bool b && b) ? (object?)TrueBrush! : (object?)FalseBrush!;
    public object ConvertBack(object value, Type _, object? __, CultureInfo ___) => throw new NotSupportedException();
}

/// <summary>
/// Picks the speaker glyph that matches the mute state, so the toolbar button
/// reads at a glance instead of always showing the same icon.
/// </summary>
public sealed class MuteIconConverter : IValueConverter
{
    public object Convert(object? value, Type _, object? __, CultureInfo ___) =>
        (value is bool muted && muted)
            ? Wpf.Ui.Controls.SymbolRegular.SpeakerOff24
            : Wpf.Ui.Controls.SymbolRegular.Speaker224;

    public object ConvertBack(object value, Type _, object? __, CultureInfo ___) => throw new NotSupportedException();
}

/// <summary>Red when muted, normal foreground otherwise.</summary>
public sealed class MuteBrushConverter : IValueConverter
{
    public Brush? MutedBrush { get; set; }
    public Brush? NormalBrush { get; set; }

    public object? Convert(object? value, Type _, object? __, CultureInfo ___) =>
        (value is bool muted && muted) ? MutedBrush : NormalBrush;

    public object ConvertBack(object value, Type _, object? __, CultureInfo ___) => throw new NotSupportedException();
}

/// <summary>Play glyph when stopped, pause glyph while playing.</summary>
public sealed class PlayPauseIconConverter : IValueConverter
{
    public object Convert(object? value, Type _, object? __, CultureInfo ___) =>
        (value is bool playing && playing)
            ? Wpf.Ui.Controls.SymbolRegular.Pause24
            : Wpf.Ui.Controls.SymbolRegular.Play24;

    public object ConvertBack(object value, Type _, object? __, CultureInfo ___) => throw new NotSupportedException();
}

/// <summary>Short caption for the HEOS connection indicator.</summary>
public sealed class HeosStatusConverter : IValueConverter
{
    public object Convert(object? value, Type _, object? __, CultureInfo ___) =>
        (value is bool ok && ok) ? "Conectado" : "No disponible";

    public object ConvertBack(object value, Type _, object? __, CultureInfo ___) => throw new NotSupportedException();
}

/// <summary>Dims the name of services the account cannot use yet.</summary>
public sealed class AvailableBrushConverter : IValueConverter
{
    public Brush? AvailableBrush { get; set; }
    public Brush? UnavailableBrush { get; set; }

    public object? Convert(object? value, Type _, object? __, CultureInfo ___) =>
        (value is bool ok && ok) ? AvailableBrush : UnavailableBrush;

    public object ConvertBack(object value, Type _, object? __, CultureInfo ___) => throw new NotSupportedException();
}

/// <summary>Shows an element only when a count is zero (empty-state hints).</summary>
public sealed class ZeroToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type _, object? __, CultureInfo ___) =>
        (value is int i && i == 0) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type _, object? __, CultureInfo ___) => throw new NotSupportedException();
}

/// <summary>Shows an element when a count is greater than zero.</summary>
public sealed class NotZeroToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type _, object? __, CultureInfo ___) =>
        (value is int i && i > 0) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type _, object? __, CultureInfo ___) => throw new NotSupportedException();
}

/// <summary>Returns card width based on grid/list mode.</summary>
public sealed class ViewWidthConverter : IValueConverter
{
    public object Convert(object? value, Type _, object? __, CultureInfo ___) =>
        (value is bool grid && grid) ? 130.0 : double.NaN;  // NaN = auto width (stretch)
    public object ConvertBack(object value, Type _, object? __, CultureInfo ___) => throw new NotSupportedException();
}

/// <summary>Returns card height based on grid/list mode.</summary>
public sealed class ViewHeightConverter : IValueConverter
{
    public object Convert(object? value, Type _, object? __, CultureInfo ___) =>
        (value is bool grid && grid) ? 140.0 : 56.0;
    public object ConvertBack(object value, Type _, object? __, CultureInfo ___) => throw new NotSupportedException();
}

/// <summary>Folder glyph for containers, note glyph for tracks.</summary>
public sealed class FolderIconConverter : IValueConverter
{
    public object Convert(object? value, Type _, object? __, CultureInfo ___) =>
        (value is bool container && container)
            ? Wpf.Ui.Controls.SymbolRegular.Folder24
            : Wpf.Ui.Controls.SymbolRegular.MusicNote124;

    public object ConvertBack(object value, Type _, object? __, CultureInfo ___) => throw new NotSupportedException();
}

/// <summary>Shows an element while a string is empty — the inverse of the usual case.</summary>
public sealed class EmptyToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type _, object? __, CultureInfo ___) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type _, object? __, CultureInfo ___) => throw new NotSupportedException();
}

public sealed class NotNullToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type _, object? __, CultureInfo ___) => value is not null;
    public object ConvertBack(object value, Type _, object? __, CultureInfo ___) => throw new NotSupportedException();
}

public sealed class StringNotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type _, object? __, CultureInfo ___) =>
        !string.IsNullOrWhiteSpace(value as string) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type _, object? __, CultureInfo ___) => throw new NotSupportedException();
}

/// <summary>Formats a numeric volume as "50.0 Â· â€“30 dB (approx)"-style label.</summary>
public sealed class VolumeLabelConverter : IValueConverter
{
    /// <summary>Unit selected in Settings. Updated by the view model.</summary>
    public static DenonRemote.Services.VolumeUnit Unit { get; set; }
        = DenonRemote.Services.VolumeUnit.Absolute;

    /// <summary>Receiver maximum, used for the percentage calculation.</summary>
    public static double Max { get; set; } = 98;

    public object Convert(object value, Type _, object? __, CultureInfo culture)
    {
        if (value is not double d) return "--";

        return Unit switch
        {
            // Denon reference: 80 on the internal scale equals 0 dB.
            DenonRemote.Services.VolumeUnit.Decibels => FormatDb(d - 80.0),
            DenonRemote.Services.VolumeUnit.Percent  => $"{Math.Round(d / (Max <= 0 ? 98 : Max) * 100)}%",
            _                                        => d.ToString("0.#", culture)
        };
    }

    private static string FormatDb(double db) => db switch
    {
        > 0 => $"+{db:0.#} dB",
        < 0 => $"{db:0.#} dB",
        _   => "0 dB"
    };

    public object ConvertBack(object value, Type _, object? __, CultureInfo ___) => throw new NotSupportedException();
}



