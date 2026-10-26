using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class LocalizationActivationSourceTests
{
  [Fact]
  public void WindowActivationRevalidatesLocalization()
  {
    var source = AppSource.Read("App.xaml.cs");

    Assert.Contains("window.Activated += OnWindowActivated;", source, StringComparison.Ordinal);
    Assert.Contains("return window;", source, StringComparison.Ordinal);
    Assert.Contains(
        "private void OnWindowActivated(object? sender, EventArgs eventArgs) =>",
        source,
        StringComparison.Ordinal);
    Assert.Contains(
        "_ = serviceProvider.GetRequiredService<LocalizationRefreshService>().RefreshChromeAsync();",
        source,
        StringComparison.Ordinal);
  }
}
