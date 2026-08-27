using Voucha.Client.Core.Api;
using Voucha.Client.Core.Settings;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelActionsTests
{
  [Fact]
  public async Task DeletePushSubscriptionAsyncDeletesTheSubscriptionWithoutReloadingSettings()
  {
    var service = new RecordingSettingsService();
    var viewModel = new SettingsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.DeletePushSubscriptionAsync(
        new WebPushSubscription(
            "__entity_type", "push-1", "user-1", "https://push.example.test", "p256dh", "auth",
            null, "device", null, null, DateTimeOffset.Parse("2026-07-01T12:00:00Z"),
            DateTimeOffset.Parse("2026-07-01T12:00:00Z")),
        TestContext.Current.CancellationToken);

    Assert.Equal(1, service.FetchMyIdentityCount);
    Assert.Equal("push-1", service.LastDeletedPushSubscriptionId);
  }

}
