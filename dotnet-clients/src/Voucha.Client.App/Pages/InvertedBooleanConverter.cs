using System.Globalization;
using Microsoft.Maui.Controls;

namespace Voucha.Client.App.Pages;

public sealed class InvertedBooleanConverter : IValueConverter
{
  public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
      value is bool flag && !flag;

  public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
      value is bool flag && !flag;
}
