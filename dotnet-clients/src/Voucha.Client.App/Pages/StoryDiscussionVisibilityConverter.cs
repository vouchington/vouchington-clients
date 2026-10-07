using System.Globalization;

namespace Voucha.Client.App.Pages;

public sealed class StoryDiscussionVisibilityConverter : IMultiValueConverter
{
  public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture) =>
      values.Length == 2 && values[0] is true && values[1] is true;

  public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
      throw new NotSupportedException();
}
