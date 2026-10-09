using Voucha.Client.Core.Api;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelActionsTests
{
  [Fact]
  public async Task DisplayedCreatedSecretIsHiddenDuringRefreshThenRestoredForSameOwner()
  {
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    var service = new RecordingSettingsService();
    using var model = new SettingsViewModel(service) { ApiKeyLabel = "Reader", ApiKeyType = "mcp" };
    await model.LoadAsync(cancellation.Token);
    model.SetApiKeyScopeSelected("mcp.user:write", true);
    await model.CreateApiKeyAsync(cancellation.Token);
    Assert.Equal("raw-key", model.ApiKeySecret);
    Assert.Null(model.ApiKeyRotationNotice);

    var started = NewSignal();
    var identity = NewSource<MyIdentityResponse>();
    service.FetchMyIdentityOverride = count => count == 3
        ? Hold(started, identity)
        : Task.FromResult(RecordingSettingsService.CreateIdentity());
    var refresh = model.LoadAsync(cancellation.Token);
    try
    {
      await started.Task.WaitAsync(cancellation.Token);
      Assert.Null(model.ApiKeySecret);
      model.DismissApiKeySecret(); // A stale hidden control cannot discard the private disclosure.
      await model.RotateApiKeyAsync(model.ApiKeys.Single(), cancellation.Token);
      Assert.Equal(0, service.RotateApiKeyCount);
    }
    finally
    {
      identity.TrySetResult(RecordingSettingsService.CreateIdentity());
      await refresh;
    }

    Assert.Equal("raw-key", model.ApiKeySecret);
    Assert.Null(model.ApiKeyRotationNotice);
    await model.RotateApiKeyAsync(model.ApiKeys.Single(), cancellation.Token);
    Assert.Equal(0, service.RotateApiKeyCount);
  }

  [Fact]
  public async Task DisplayedRotatedSecretIsClearedWhenRefreshConfirmsAnotherOwner()
  {
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    var service = new RecordingSettingsService();
    using var model = new SettingsViewModel(service);
    await model.LoadAsync(cancellation.Token);
    await model.RotateApiKeyAsync(model.ApiKeys.Single(), cancellation.Token);
    Assert.Equal("replacement-raw", model.ApiKeySecret);
    Assert.NotNull(model.ApiKeyRotationNotice);

    var started = NewSignal();
    var identity = NewSource<MyIdentityResponse>();
    service.FetchMyIdentityOverride = count => count == 2
        ? Hold(started, identity)
        : Task.FromResult(RecordingSettingsService.CreateIdentity());
    var refresh = model.LoadAsync(cancellation.Token);
    try
    {
      await started.Task.WaitAsync(cancellation.Token);
      Assert.Null(model.ApiKeySecret);
      Assert.Null(model.ApiKeyRotationNotice);
    }
    finally
    {
      identity.TrySetResult(RecordingSettingsService.CreateIdentity("user-2"));
      await refresh;
    }

    Assert.Null(model.ApiKeySecret);
    Assert.Null(model.ApiKeyRotationNotice);
    Assert.Null(model.ApiKeySecretDisplay);
  }
}
