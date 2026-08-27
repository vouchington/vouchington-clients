using System.Globalization;

namespace Voucha.Client.App.Pages;

public sealed class FollowerDistributionVisibilityConverter : IMultiValueConverter
{
  public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
  {
    if (values.Length < 2 || values[0] is not true || values[1] is not true) return false;
    return values.Length < 4 || values[3] is string currentUserId &&
        (values[2] is not string creatorId || !string.Equals(creatorId, currentUserId, StringComparison.Ordinal));
  }

  public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
      throw new NotSupportedException();
}
