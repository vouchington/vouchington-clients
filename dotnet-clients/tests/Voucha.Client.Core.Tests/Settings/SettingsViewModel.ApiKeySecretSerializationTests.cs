using Voucha.Client.Core.Api;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelActionsTests
{
  [Fact]
  public async Task HeldRotationBlocksOtherKeyAndCreateUntilItsSecretIsDismissed()
  {
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    var service = new RecordingSettingsService();
    using var model = new SettingsViewModel(service) { ApiKeyLabel = "Reader", ApiKeyType = "mcp" };
    await model.LoadAsync(cancellation.Token);
    model.SetApiKeyScopeSelected("mcp.user:write", true);
    var firstKey = model.ApiKeys.Single();
    var secondKey = firstKey with { Id = "key-b" };
    var started = NewSignal();
    var firstResponse = NewSource<ApiKeyCreationResponse>();
    service.RotateApiKeyOverride = id => id == firstKey.Id
        ? HoldRotation(started, firstResponse)
        : Task.FromResult(new ApiKeyCreationResponse(secondKey, "raw-b"));
    var createDispatches = 0;
    service.CreateApiKeyOverride = () =>
    {
      createDispatches++;
      return Task.FromResult(new ApiKeyCreationResponse(firstKey, "created-raw"));
    };

    var first = model.RotateApiKeyAsync(firstKey, cancellation.Token);
    try
    {
      await started.Task.WaitAsync(cancellation.Token);
      Assert.False(model.CanCreateApiKey);
      Assert.All(model.LocalizedApiKeys, row => Assert.False(row.CanRotateNow));
      await model.RotateApiKeyAsync(secondKey, cancellation.Token);
      await model.CreateApiKeyAsync(cancellation.Token);
      Assert.Equal(1, service.RotateApiKeyCount);
      Assert.Equal(0, createDispatches);
      Assert.Null(model.ApiKeySecret);
    }
    finally
    {
      firstResponse.TrySetResult(new ApiKeyCreationResponse(firstKey, "raw-a"));
      await first;
    }

    Assert.Equal("raw-a", model.ApiKeySecret);
    await model.RotateApiKeyAsync(secondKey, cancellation.Token);
    await model.CreateApiKeyAsync(cancellation.Token);
    Assert.Equal(1, service.RotateApiKeyCount);
    Assert.Equal(0, createDispatches);
    model.DismissApiKeySecret();
    Assert.True(model.CanCreateApiKey);
    await model.RotateApiKeyAsync(secondKey, cancellation.Token);
    Assert.Equal(2, service.RotateApiKeyCount);
    Assert.Equal("raw-b", model.ApiKeySecret);
  }

  [Fact]
  public async Task HeldCreateBlocksRotationAndAnotherCreateUntilItsSecretIsDismissed()
  {
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    var service = new RecordingSettingsService();
    using var model = new SettingsViewModel(service) { ApiKeyLabel = "Reader", ApiKeyType = "mcp" };
    await model.LoadAsync(cancellation.Token);
    model.SetApiKeyScopeSelected("mcp.user:write", true);
    var key = model.ApiKeys.Single();
    var started = NewSignal();
    var response = NewSource<ApiKeyCreationResponse>();
    var createDispatches = 0;
    service.CreateApiKeyOverride = () =>
    {
      createDispatches++;
      started.TrySetResult(true);
      return response.Task;
    };

    var first = model.CreateApiKeyAsync(cancellation.Token);
    try
    {
      await started.Task.WaitAsync(cancellation.Token);
      Assert.False(model.CanCreateApiKey);
      Assert.False(Assert.Single(model.LocalizedApiKeys).CanRotateNow);
      await model.CreateApiKeyAsync(cancellation.Token);
      await model.RotateApiKeyAsync(key, cancellation.Token);
      Assert.Equal(1, createDispatches);
      Assert.Equal(0, service.RotateApiKeyCount);
    }
    finally
    {
      response.TrySetResult(new ApiKeyCreationResponse(key, "created-raw"));
      await first;
    }

    Assert.Equal("created-raw", model.ApiKeySecret);
    await model.RotateApiKeyAsync(key, cancellation.Token);
    Assert.Equal(0, service.RotateApiKeyCount);
    model.DismissApiKeySecret();
    Assert.True(Assert.Single(model.LocalizedApiKeys).CanRotateNow);
    await model.RotateApiKeyAsync(key, cancellation.Token);
    Assert.Equal(1, service.RotateApiKeyCount);
  }
}
