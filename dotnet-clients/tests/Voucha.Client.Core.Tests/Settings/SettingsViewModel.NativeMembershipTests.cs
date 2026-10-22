using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Settings;
using Voucha.Client.Core.Tests.NativeBilling;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelTests
{
  [Fact]
  public async Task LifecycleFixtureStaysFreeAndShowsPendingOutcomesAsText()
  {
    var service = new FakeSettingsService
    {
      MembershipOverviewResponse = JsonSerializer.Deserialize<NativeMembershipOverview>(
          MembershipFixture.Json("native.memberships.me.lifecycle.default"),
          VouchaApiJson.Options),
    };
    var viewModel = new SettingsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Current plan: Free", viewModel.MembershipSummary);
    Assert.True(viewModel.MembershipPlanOptions.Single(item => item.Slug == "free").IsCurrentPlan);
    Assert.False(viewModel.MembershipPlanOptions.Single(item => item.Slug == "pro").IsCurrentPlan);
    Assert.Equal(
        "Price unavailable",
        viewModel.MembershipPlanOptions.Single(item => item.Slug == "plus").PriceOptions.Single().Label);
    var lines = string.Join('\n', viewModel.MembershipLifecycleLines.Select(line => line.Text));
    Assert.Contains("automatic_refund", lines, StringComparison.Ordinal);
    Assert.Contains("competing_direct_source", lines, StringComparison.Ordinal);
    Assert.Contains("wrong_account", lines, StringComparison.Ordinal);
    Assert.Contains("2027-01-01T00:00:00.000Z", lines, StringComparison.Ordinal);
    Assert.Contains("apple_subscriptions", lines, StringComparison.Ordinal);
    Assert.DoesNotContain("http", lines, StringComparison.Ordinal);
    Assert.DoesNotContain("500", lines, StringComparison.Ordinal);

    viewModel.NoteMembershipOverviewStale();
    Assert.Contains(
        "Native purchase and billing management are not available yet.",
        viewModel.MembershipLifecycleLines[0].Text,
        StringComparison.Ordinal);
    Assert.Equal("Current plan: Free", viewModel.MembershipSummary);
  }
}
