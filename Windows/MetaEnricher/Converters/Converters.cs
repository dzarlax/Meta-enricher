using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using MetaEnricher.Models;
using Windows.UI;

namespace MetaEnricher.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool b = value is bool bv && bv;
        bool invert = parameter is string s && s == "invert";
        if (invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is Visibility v && v == Visibility.Visible;
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool isNull = value == null || (value is string s && string.IsNullOrWhiteSpace(s));
        bool invert = parameter is string p && p == "invert";
        if (invert) isNull = !isNull;
        return isNull ? Visibility.Collapsed : Visibility.Visible;
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class EnrichmentStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is SessionEnrichmentStatus status)
        {
            return status switch
            {
                SessionEnrichmentStatus.Enriched => new SolidColorBrush(Color.FromArgb(255, 34, 197, 94)),   // green
                SessionEnrichmentStatus.Partial => new SolidColorBrush(Color.FromArgb(255, 255, 185, 56)),   // amber
                SessionEnrichmentStatus.Pending => new SolidColorBrush(Color.FromArgb(255, 156, 163, 175)),  // gray
                _ => new SolidColorBrush(Color.FromArgb(255, 75, 85, 99)),
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class KeywordsToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is List<string> kws) return string.Join(", ", kws);
        return "";
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is string s)
            return s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        return new List<string>();
    }
}

public class IsEnrichedToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b && b)
            return new SolidColorBrush(Color.FromArgb(255, 34, 197, 94));
        return new SolidColorBrush(Color.FromArgb(255, 75, 85, 99));
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class IntToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool visible = value is int i && i > 0;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class RatingToStarsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        int rating = value is int r ? r : 0;
        return string.Join("", Enumerable.Range(0, 5).Select(i => i < rating ? "★" : "☆"));
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class StringFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (parameter is string fmt && value != null)
            return string.Format(fmt, value);
        return value?.ToString() ?? "";
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class DoubleToGridLengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double d) return new GridLength(d);
        return new GridLength(120);
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is GridLength gl) return gl.Value;
        return 120.0;
    }
}

public class BoolNegationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool b ? !b : true;
    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is bool b ? !b : false;
}

public class SelectedToBorderBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Amber = new(Color.FromArgb(255, 255, 185, 56));
    private static readonly SolidColorBrush Transparent = new(Color.FromArgb(0, 0, 0, 0));

    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool b && b ? Amber : Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
