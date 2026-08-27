using Voucha.Client.Core.Api;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelActionsTests
{
  [Fact]
  public async Task CheckoutMembershipAsyncUsesTheCheckoutUrls()
  {
    var service = new RecordingSettingsService();
    var viewModel = new SettingsViewModel(service);

    var url = await viewModel.CheckoutMembershipAsync(
        new MembershipSku("sku-1", "pro", new Money(1500, "usd"), "month", "price-1"),
        TestContext.Current.CancellationToken);

    Assert.Equal("https://checkout/", url);
    Assert.NotNull(service.LastCheckoutBody);
    Assert.Equal("price-1", service.LastCheckoutBody!.PriceId);
    Assert.Equal("/my/membership", service.LastCheckoutBody.SuccessUrl.OriginalString);
    Assert.Equal("/my/membership", service.LastCheckoutBody.CancelUrl.OriginalString);
  }

  [Fact]
  public async Task OpenMembershipPortalAsyncUsesTheReturnUrl()
  {
    var service = new RecordingSettingsService();
    var viewModel = new SettingsViewModel(service);

    var url = await viewModel.OpenMembershipPortalAsync(TestContext.Current.CancellationToken);

    Assert.Equal("https://portal/", url);
    Assert.NotNull(service.LastPortalBody);
    Assert.Equal("/my/membership", service.LastPortalBody!.ReturnUrl.OriginalString);
  }

  [Fact]
  public async Task CancelMembershipAsyncReloadsTheSurface()
  {
    var service = new RecordingSettingsService();
    var viewModel = new SettingsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.CancelMembershipAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, service.FetchMyIdentityCount);
    Assert.Equal(1, service.CancelMembershipCount);
    Assert.Equal("Current plan: Pro · Active", viewModel.MembershipSummary);
  }
}
