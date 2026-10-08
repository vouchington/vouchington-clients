using System.Globalization;
using Microsoft.Maui.Controls;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class UiLocalizedValueExtensionTests
{
  [Fact]
  public void ConverterWaitsForAValueBeforeFormattingDate()
  {
    var converter = new UiLocalizedValueConverter(UiLocalization.English);

    Assert.Equal(string.Empty, converter.Convert([null!, 0], typeof(string), "dateTime", CultureInfo.InvariantCulture));
    Assert.Equal(string.Empty, converter.Convert([BindableProperty.UnsetValue, 0], typeof(string), "dateTime", CultureInfo.InvariantCulture));
    Assert.NotEqual(string.Empty, converter.Convert([DateTimeOffset.UnixEpoch, 0], typeof(string), "dateTime", CultureInfo.InvariantCulture));
    Assert.Throws<ArgumentException>(() => converter.Convert(["invalid date", 0], typeof(string), "dateTime", CultureInfo.InvariantCulture));
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData(" ")]
  public void RequiresAnExplicitNonEmptyFormat(string? format)
  {
    var extension = new UiLocalizedValueExtension { Format = format };

    Assert.ThrowsAny<ArgumentException>(() => extension.ProvideValue(null!));
  }
}
