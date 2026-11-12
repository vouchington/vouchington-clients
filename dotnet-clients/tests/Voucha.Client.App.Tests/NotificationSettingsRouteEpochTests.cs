using Voucha.Client.App;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class NotificationSettingsRouteEpochTests
{
  [Fact]
  public void DuplicateRouteApplicationsAndLoadCompletionsActivateOnlyOnce()
  {
    var epoch = new NotificationSettingsRouteEpoch();
    epoch.Apply(true);
    var firstLoadEpoch = epoch.Value;
    epoch.Apply(true);
    var duplicateLoadEpoch = epoch.Value;

    Assert.Equal(firstLoadEpoch, duplicateLoadEpoch);
    Assert.True(epoch.TryActivate(firstLoadEpoch));
    Assert.False(epoch.TryActivate(duplicateLoadEpoch));
    epoch.Apply(true);
    Assert.False(epoch.TryActivate(epoch.Value));
  }

  [Fact]
  public void DepartureRejectsPendingLoadAndReentryAllowsANewActivation()
  {
    var epoch = new NotificationSettingsRouteEpoch();
    epoch.Apply(true);
    var firstLoadEpoch = epoch.Value;
    Assert.True(epoch.TryActivate(firstLoadEpoch));

    epoch.Apply(false);
    Assert.False(epoch.TryActivate(firstLoadEpoch));
    Assert.False(epoch.TryActivate(epoch.Value));
    epoch.Apply(true);

    Assert.NotEqual(firstLoadEpoch, epoch.Value);
    Assert.False(epoch.TryActivate(firstLoadEpoch));
    Assert.True(epoch.TryActivate(epoch.Value));
    Assert.False(epoch.TryActivate(epoch.Value));
  }

  [Fact]
  public void DisappearanceInvalidatesPendingLoadAndAllowsActivationOnReturn()
  {
    var epoch = new NotificationSettingsRouteEpoch();
    epoch.Apply(true);
    var pendingLoadEpoch = epoch.Value;
    epoch.Invalidate();

    Assert.False(epoch.TryActivate(pendingLoadEpoch));
    epoch.Apply(true);
    Assert.True(epoch.TryActivate(epoch.Value));
    var activatedEpoch = epoch.Value;
    epoch.Invalidate();
    Assert.False(epoch.TryActivate(activatedEpoch));
    epoch.Apply(true);
    Assert.True(epoch.TryActivate(epoch.Value));
  }

  [Fact]
  public void UnfocusedPageCannotActivate()
  {
    var epoch = new NotificationSettingsRouteEpoch();
    Assert.False(epoch.TryActivate(epoch.Value));
    epoch.Apply(false);
    Assert.False(epoch.TryActivate(epoch.Value));
  }
}
