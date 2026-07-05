using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using SpaceSensorDesigner.App.Rendering;
using SpaceSensorDesigner.Core.Models;

namespace SpaceSensorDesigner.App.Converters;

/// <summary>Returns true when the bound enum value equals the ConverterParameter (enum name).</summary>
public sealed class EnumMatchConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value != null && parameter != null &&
           string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);

    // For ToggleButton.IsChecked two-way: only act when turning on.
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true && parameter != null
            ? Enum.Parse(targetType, parameter.ToString()!, true)
            : Binding.DoNothing;
}

/// <summary>bool → Visibility (true = Visible). Invertible via parameter "invert".</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool b = value is true;
        if (parameter is string s && s.Equals("invert", StringComparison.OrdinalIgnoreCase)) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

/// <summary>int &gt; threshold (ConverterParameter, default 1) → Visible, else Collapsed.</summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int threshold = parameter is string s && int.TryParse(s, out var t) ? t : 1;
        return value is int n && n > threshold ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Boolean negation (true ↔ false). Used for IsEnabled = !IsOptimizing, etc.</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not true;
}

/// <summary>
/// Null → Collapsed, otherwise Visible. Pass ConverterParameter="invert" to flip it (Visible only
/// when the value is null) — used for the "nothing selected" empty-state panel.
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool visibleWhenPresent = !(parameter is string s && s.Equals("invert", StringComparison.OrdinalIgnoreCase));
        bool hasValue = value != null;
        bool show = visibleWhenPresent ? hasValue : !hasValue;
        return show ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>RoomType → a solid brush from the shared palette (used for the room-type swatches).</summary>
public sealed class RoomTypeToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is RoomType rt ? Palette.Brush(Palette.RoomColor(rt)) : Brushes.Transparent;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
