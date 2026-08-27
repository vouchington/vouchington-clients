using System.Globalization;
using Microsoft.Maui.Controls;
using Voucha.Client.Core.Api;

namespace Voucha.Client.App.Pages;

public sealed class VoteClearEligibilityConverter : IMultiValueConverter
{
  public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture) =>
      values.Length == 2 && values[0] is ElectionVoteChoice && values[1] is true;

  public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
      throw new NotSupportedException();
}
