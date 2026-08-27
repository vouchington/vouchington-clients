using Voucha.Client.App.Pages;
using Voucha.Client.Core.Moderation;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class CommunitySectionInitialRouteStateTests
{
  [Fact]
  public void DeepLinkedRangeAppliesOnceAndDoesNotOverwriteTheRangeSelectedBeforeReturning()
  {
    var routeState = new CommunitySectionInitialRouteState
    {
      ModerationTransparencyRange = ModerationTransparencyRange.All,
    };

    var selectedRange = ModerationTransparencyRange.Default;
    if (routeState.TryTakeModerationTransparencyRange(out var initialRange))
    {
      selectedRange = ModerationTransparencyRange.ParseOrDefault(initialRange);
    }
    selectedRange = ModerationTransparencyRange.SevenDays;

    if (routeState.TryTakeModerationTransparencyRange(out initialRange))
    {
      selectedRange = ModerationTransparencyRange.ParseOrDefault(initialRange);
    }

    Assert.Equal(ModerationTransparencyRange.SevenDays, selectedRange);
  }

  [Fact]
  public void ARepeatedDeepLinkRangeIsAppliedOnTheNextInitialAppearance()
  {
    var routeState = new CommunitySectionInitialRouteState
    {
      ModerationTransparencyRange = ModerationTransparencyRange.SevenDays,
    };

    Assert.True(routeState.TryTakeModerationTransparencyRange(out var firstRange));
    routeState.ModerationTransparencyRange = ModerationTransparencyRange.SevenDays;

    Assert.True(routeState.TryTakeModerationTransparencyRange(out var nextRange));
    Assert.Equal(ModerationTransparencyRange.SevenDays, firstRange);
    Assert.Equal(ModerationTransparencyRange.SevenDays, nextRange);
  }
}
