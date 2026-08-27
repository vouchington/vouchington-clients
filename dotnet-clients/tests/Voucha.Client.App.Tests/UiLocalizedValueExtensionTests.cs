using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class UiLocalizedValueExtensionTests
{
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
