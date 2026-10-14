using System.Globalization;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;

namespace Voucha.Client.App.Pages;

public sealed class PlacementImageSourceConverter(AppConfig config, int width) : IValueConverter
{
  public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
      value is TopicImagePlacement { } placement &&
      config.ImageUrlForPlacement(placement.PlacementId, placement.PlacementRevision, placement.ImageId, width) is { } url
          ? ImageSource.FromUri(url)
          : null;

  public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
      throw new NotSupportedException();
}
