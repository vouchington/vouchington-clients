using Voucha.Client.App;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class NotificationSettingsRouteEpochTests
{
  [Fact]
  public void RepeatedFocusedRouteApplicationsShareAnEpochWhileDepartureInvalidatesIt()
  {
    var epoch = new NotificationSettingsRouteEpoch();

    epoch.Apply(true);
    var focusedEpoch = epoch.Value;
    epoch.Apply(true);

    Assert.Equal(focusedEpoch, epoch.Value);
    epoch.Apply(false);
    Assert.NotEqual(focusedEpoch, epoch.Value);
    var departedEpoch = epoch.Value;
    epoch.Apply(true);
    Assert.NotEqual(departedEpoch, epoch.Value);
    var reenteredEpoch = epoch.Value;
    epoch.Invalidate();
    Assert.NotEqual(reenteredEpoch, epoch.Value);
  }
}
