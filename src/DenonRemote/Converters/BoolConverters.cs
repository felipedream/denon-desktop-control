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
    public object Convert(object value, Type _, object? __, CultureInfo culture)
    {
        if (value is double d)
            return d.ToString("0.0", culture);
        return "-";
    }
    public object ConvertBack(object value, Type _, object? __, CultureInfo ___) => throw new NotSupportedException();
}



