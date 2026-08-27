using System.Globalization;
using Microsoft.Maui.Controls;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.LandingPages;

namespace Voucha.Client.App.Pages;

public sealed class LandingPageItemDescriptionConverter : IValueConverter
{
  public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
      value is LandingPageItem item ? UiCopy.Resolve(item.DescribeText()) : string.Empty;

  public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
      throw new NotSupportedException();
}
