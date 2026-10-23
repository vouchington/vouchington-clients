using Voucha.Client.Core;
using Xunit;

namespace Voucha.Client.Core.Tests;

public sealed class SentryLaunchPlanTests
{
  [Fact]
  public void MissingDsnDisablesDebugAndReleaseWithoutThrowing()
  {
    var debug = SentryLaunchPlan.Resolve(new Dictionary<string, string?> { ["VOUCHA_SENTRY_DSN"] = "  " }, new Dictionary<string, string?>(), releaseConfiguration: false, "1.0+1", "  ");
    var release = SentryLaunchPlan.Resolve(null, new Dictionary<string, string?> { ["VOUCHA_SENTRY_DSN"] = "" }, releaseConfiguration: true, "1.0+1", null);

    Assert.False(debug.StartsSdk);
    Assert.False(release.StartsSdk);
    Assert.Equal(0, debug.TracesSampleRate);
    Assert.Equal("debug", debug.EnvironmentName);
    Assert.Equal("release", release.EnvironmentName);
    Assert.False(release.SendDefaultPii);
    Assert.Null(debug.AccountId);
  }

  [Fact]
  public void EnvironmentDsnOverridesPackagedMetadata()
  {
    var plan = SentryLaunchPlan.Resolve(
        new Dictionary<string, string?> { ["VOUCHA_SENTRY_DSN"] = " https://public@example.invalid/1 " },
        new Dictionary<string, string?> { ["VOUCHA_SENTRY_DSN"] = "https://packaged@example.invalid/2" },
        releaseConfiguration: true,
        "1.0+1",
        " account-1 ");

    Assert.True(plan.StartsSdk);
    Assert.Equal("https://public@example.invalid/1", plan.Dsn);
    Assert.Equal("account-1", plan.AccountId);
    Assert.Equal(SentryLaunchPlan.EnabledTracesSampleRate, plan.TracesSampleRate);
    Assert.False(plan.SendDefaultPii);
  }

  [Fact]
  public void AccountBindingSetsSwitchesAndClearsTheOpaqueId()
  {
    var signedIn = SentryAccountDecision.Update(null, "account-1");
    var unchanged = SentryAccountDecision.Update("account-1", " account-1 ");
    var switched = SentryAccountDecision.Update("account-1", "account-2");
    var signedOut = SentryAccountDecision.Update("account-2", " ");

    Assert.Equal(SentryAccountAction.Set, signedIn.Action);
    Assert.Equal("account-1", signedIn.AccountId);
    Assert.Equal(SentryAccountAction.Unchanged, unchanged.Action);
    Assert.Equal(SentryAccountAction.Set, switched.Action);
    Assert.Equal("account-2", switched.AccountId);
    Assert.Equal(SentryAccountAction.Clear, signedOut.Action);
    Assert.Null(signedOut.AccountId);
  }

  [Fact]
  public void InvalidDsnDisablesSdk()
  {
    var plan = SentryLaunchPlan.Resolve(
        new Dictionary<string, string?> { ["VOUCHA_SENTRY_DSN"] = "not-a-dsn" },
        null,
        releaseConfiguration: false,
        "1.0+1",
        null);

    Assert.False(plan.StartsSdk);
    Assert.Null(plan.Dsn);
  }
}
