using Voucha.Client.App;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class CatalystGuiLaunchOptionsTests
{
  [Fact]
  public void OrdinaryLaunchDoesNotActivateFixturesOrChangeRootNavigation()
  {
    var options = CatalystGuiLaunchOptions.FromArguments(["Voucha", "unrelated"]);
    Assert.False(options.IsActive);
    Assert.False(options.OpenNotifications);
  }

  [Fact]
  public void NotificationLaunchRetainsTheExistingSettingsNavigation()
  {
    var options = CatalystGuiLaunchOptions.FromArguments(["Voucha", "--ui-testing-launch-smoke"]);
    Assert.True(options.IsActive);
    Assert.True(options.OpenNotifications);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void RootShellLaunchKeepsTheProductionInitialSurface(bool includeNotificationArgument)
  {
    var arguments = new List<string> { "Voucha", "--ui-testing-root-shell-smoke" };
    if (includeNotificationArgument) arguments.Add("--ui-testing-launch-smoke");
    var options = CatalystGuiLaunchOptions.FromArguments(arguments);
    Assert.True(options.IsActive);
    Assert.False(options.OpenNotifications);
  }

  [Fact]
  public void NullArgumentListFailsClosed() =>
      Assert.Throws<ArgumentNullException>(() => CatalystGuiLaunchOptions.FromArguments(null!));
}
