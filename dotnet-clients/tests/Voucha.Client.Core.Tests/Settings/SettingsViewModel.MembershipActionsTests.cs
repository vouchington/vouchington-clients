using Voucha.Client.Core.Api;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelActionsTests
{
  [Fact]
  public async Task CheckoutMembershipAsyncStaysClosed()
  {
    var service = new RecordingSettingsService();
    var viewModel = new SettingsViewModel(service);

    var url = await viewModel.CheckoutMembershipAsync(
        new MembershipSku("sku-1", "pro", new Money(1500, "usd"), "month", "price-1"),
        TestContext.Current.CancellationToken);

    Assert.Equal(string.Empty, url);
    Assert.Null(service.LastCheckoutBody);
    Assert.Equal(
        "Native purchase and billing management are not available yet.",
        viewModel.MembershipActionNotice);
  }

  [Fact]
  public async Task OpenMembershipPortalAsyncStaysClosed()
  {
    var service = new RecordingSettingsService();
    var viewModel = new SettingsViewModel(service);

    var url = await viewModel.OpenMembershipPortalAsync(TestContext.Current.CancellationToken);

    Assert.Equal(string.Empty, url);
    Assert.Null(service.LastPortalBody);
    Assert.Equal(
        "Native purchase and billing management are not available yet.",
        viewModel.MembershipActionNotice);
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
