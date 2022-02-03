using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using SipgateVirtualFax.Core.Sipgate;

namespace SipGateVirtualFaxGui;

public class BoolToVisibilityConverter : MarkupExtension, IValueConverter
{
    public BoolToVisibilityConverter()
    {
        TrueValue = Visibility.Visible;
        FalseValue = Visibility.Collapsed;
    }

    public Visibility TrueValue { get; set; }
    public Visibility FalseValue { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var val = System.Convert.ToBoolean(value);
        return val ? TrueValue : FalseValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return TrueValue.Equals(value);
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return this;
    }
}
public class FaxStatusToProgressConverter : MarkupExtension, IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is FaxStatus status)
        {
            return status switch
            {
                FaxStatus.Pending => 1,
                FaxStatus.Sending => 2,
                FaxStatus.SuccessfullySent => 3,
                FaxStatus.Failed => 3,
                FaxStatus.Unknown => 3,
                _ => 0
            };
        }

        throw new Exception($"Illegal value received: {value}");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return FaxStatus.Unknown;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return this;
    }
}
public class FaxStatusToBrushConverter : MarkupExtension, IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is FaxStatus status)
        {
            return status switch
            {
                FaxStatus.Pending => Brushes.Green,
                FaxStatus.Sending => Brushes.Green,
                FaxStatus.SuccessfullySent => Brushes.Green,
                FaxStatus.Failed => Brushes.Red,
                FaxStatus.Unknown => Brushes.Orange,
                _ => Brushes.Orange
            };
        }

        throw new Exception($"Illegal value received: {value}");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return FaxStatus.Unknown;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return this;
    }
}