using System.Globalization;
using Voucha.Client.App.Support;

namespace Voucha.Client.App.Pages;

public sealed class LocalImagePreviewSourceConverter : IValueConverter
{
  public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
      value is ReadOnlyMemory<byte> { Length: > 0 } bytes
          ? LocalImagePreview.SourceFromBytes(bytes.ToArray())
          : null;

  public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
      throw new NotSupportedException();
}
