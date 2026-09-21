using Voucha.Client.Core.Api;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelTests
{
  [Fact]
  public async Task LoadAsyncMarksFreeAsTheCurrentPlanWhenNoMembershipExists()
  {
    var service = new FakeSettingsService { MembershipResponse = null };
    var viewModel = new SettingsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Current plan: Free", viewModel.MembershipSummary);
    Assert.True(viewModel.MembershipPlanOptions.Single(item => item.Slug == "free").IsCurrentPlan);
    Assert.False(viewModel.MembershipPlanOptions.Single(item => item.Slug == "plus").IsCurrentPlan);
  }

  [Fact]
  public async Task LoadAsyncHumanizesActiveMembershipStatus()
  {
    var service = new FakeSettingsService { MembershipResponse = new MembershipResponse(FakeSettingsService.CreateMembership(status: "past_due")) };
    var viewModel = new SettingsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Current plan: Pro · Past due", viewModel.MembershipSummary);
    Assert.True(viewModel.MembershipPlanOptions.Single(item => item.Slug == "pro").IsCurrentPlan);
  }

  [Fact]
  public async Task LoadAsyncTreatsPausedMembershipAsFreeEntitlement()
  {
    var service = new FakeSettingsService { MembershipResponse = new MembershipResponse(FakeSettingsService.CreateMembership(status: "paused")) };
    var viewModel = new SettingsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Current plan: Free · Paused", viewModel.MembershipSummary);
    Assert.True(viewModel.MembershipPlanOptions.Single(item => item.Slug == "free").IsCurrentPlan);
    Assert.False(viewModel.MembershipPlanOptions.Single(item => item.Slug == "pro").IsCurrentPlan);
  }

  [Fact]
  public async Task MembershipPricesUseTheCurrencyMinorUnitExponent()
  {
    var service = new FakeSettingsService
    {
      MembershipPlansResponse = new MembershipPlansResponse(
          [new MembershipCatalogProduct(
              "sku-jpy",
              "pro",
              "month",
              [new MembershipCatalogProvider("stripe", "test", "voucha-web", "price-jpy", null, null, null, new Money(1234, "jpy"))])]),
    };
    var viewModel = new SettingsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var pro = viewModel.MembershipPlanOptions.Single(item => item.Slug == "pro");
    Assert.Equal("JPY\u00A01,234 monthly", pro.PriceOptions.Single().Label);
  }
}
