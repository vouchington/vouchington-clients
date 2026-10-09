using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelActionsTests
{
  [Fact]
  public async Task HeldCreateCannotPublishRawKeyAfterAnotherOwnerIsConfirmed()
  {
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    var service = new RecordingSettingsService();
    using var viewModel = new SettingsViewModel(service) { ApiKeyLabel = "Reader", ApiKeyType = "mcp" };
    await viewModel.LoadAsync(cancellation.Token);
    viewModel.SetApiKeyScopeSelected("mcp.user:write", true);
    Assert.True(viewModel.CanCreateApiKey);
    var started = NewSignal();
    var response = NewSource<ApiKeyCreationResponse>();
    service.CreateApiKeyOverride = () =>
    {
      started.TrySetResult(true);
      return response.Task;
    };
    service.FetchMyIdentityOverride = count => Task.FromResult(
        RecordingSettingsService.CreateIdentity(count == 1 ? "user-1" : "user-2"));

    var create = viewModel.CreateApiKeyAsync(cancellation.Token);
    try
    {
      await started.Task.WaitAsync(cancellation.Token);
      await viewModel.LoadAsync(cancellation.Token);
      Assert.Null(viewModel.ApiKeySecret);
    }
    finally
    {
      response.TrySetResult(new ApiKeyCreationResponse(viewModel.ApiKeys.Single(), "raw-user-1"));
      await create;
    }

    Assert.Null(viewModel.ApiKeySecret);
    Assert.Null(viewModel.ApiKeySecretDisplay);
    Assert.Equal("Reader", viewModel.ApiKeyLabel);
  }

  [Fact]
  public async Task CreateRefreshCannotPublishOldRawKeyWhenItConfirmsAnotherOwner()
  {
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    var service = new RecordingSettingsService();
    using var viewModel = new SettingsViewModel(service) { ApiKeyLabel = "Reader", ApiKeyType = "mcp" };
    await viewModel.LoadAsync(cancellation.Token);
    viewModel.SetApiKeyScopeSelected("mcp.user:write", true);
    var identityStarted = NewSignal();
    var identityResponse = NewSource<MyIdentityResponse>();
    service.FetchMyIdentityOverride = count => count == 2
        ? Hold(identityStarted, identityResponse)
        : Task.FromResult(RecordingSettingsService.CreateIdentity());

    var create = viewModel.CreateApiKeyAsync(cancellation.Token);
    try
    {
      await identityStarted.Task.WaitAsync(cancellation.Token);
      Assert.Null(viewModel.ApiKeySecret);
    }
    finally
    {
      identityResponse.TrySetResult(RecordingSettingsService.CreateIdentity("user-2"));
      await create;
    }

    Assert.Null(viewModel.ApiKeySecret);
    Assert.Null(viewModel.ApiKeySecretDisplay);
    Assert.Equal("user-2", service.LastFetchedUserId);
  }

  [Fact]
  public async Task CreatedSecretWaitsForSameOwnerAfterTransientIdentityFailure()
  {
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    var service = new RecordingSettingsService();
    using var viewModel = new SettingsViewModel(service) { ApiKeyLabel = "Reader", ApiKeyType = "mcp" };
    await viewModel.LoadAsync(cancellation.Token);
    viewModel.SetApiKeyScopeSelected("mcp.user:write", true);
    service.FetchMyIdentityOverride = count => count == 2
        ? Task.FromException<MyIdentityResponse>(new HttpRequestException("temporary identity outage"))
        : Task.FromResult(RecordingSettingsService.CreateIdentity());

    await viewModel.CreateApiKeyAsync(cancellation.Token);
    Assert.Null(viewModel.ApiKeySecret);
    Assert.Null(viewModel.ApiKeyRotationNotice);
    Assert.False(viewModel.CanCreateApiKey);

    await viewModel.LoadAsync(cancellation.Token);
    Assert.Equal("raw-key", viewModel.ApiKeySecret);
    Assert.Null(viewModel.ApiKeyRotationNotice);
  }

  [Fact]
  public async Task CreateButtonEligibilityTracksUnconfirmedIdentityAndDisposal()
  {
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    var service = new RecordingSettingsService();
    var viewModel = new SettingsViewModel(service) { ApiKeyLabel = "Reader", ApiKeyType = "mcp" };
    await viewModel.LoadAsync(cancellation.Token);
    viewModel.SetApiKeyScopeSelected("mcp.user:write", true);
    Assert.True(viewModel.CanCreateApiKey);
    var started = NewSignal();
    var identity = NewSource<MyIdentityResponse>();
    service.FetchMyIdentityOverride = count => count == 2
        ? Hold(started, identity) : Task.FromResult(RecordingSettingsService.CreateIdentity());
    var notifications = new List<string?>();
    viewModel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

    var refresh = viewModel.LoadAsync(cancellation.Token);
    try
    {
      await started.Task.WaitAsync(cancellation.Token);
      Assert.False(viewModel.CanCreateApiKey);
      Assert.False(Assert.Single(viewModel.LocalizedApiKeys).CanRotateNow);
    }
    finally
    {
      identity.TrySetResult(RecordingSettingsService.CreateIdentity());
      await refresh;
    }

    viewModel.SetApiKeyScopeSelected("mcp.user:write", true);
    Assert.True(viewModel.CanCreateApiKey);
    Assert.True(Assert.Single(viewModel.LocalizedApiKeys).CanRotateNow);
    viewModel.Dispose();
    Assert.False(viewModel.CanCreateApiKey);
    Assert.Contains(nameof(viewModel.CanCreateApiKey), notifications);
  }

  [Fact]
  public async Task StaleCreateUnauthorizedCannotInvalidateTheNewOwner()
  {
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    var service = new RecordingSettingsService();
    using var viewModel = new SettingsViewModel(service) { ApiKeyLabel = "Reader", ApiKeyType = "mcp" };
    await viewModel.LoadAsync(cancellation.Token);
    viewModel.SetApiKeyScopeSelected("mcp.user:write", true);
    var response = NewSource<ApiKeyCreationResponse>();
    service.CreateApiKeyOverride = () => response.Task;
    service.FetchMyIdentityOverride = count => Task.FromResult(
        RecordingSettingsService.CreateIdentity(count == 1 ? "user-1" : "user-2"));
    var create = viewModel.CreateApiKeyAsync(cancellation.Token);
    try
    {
      await viewModel.LoadAsync(cancellation.Token);
    }
    finally
    {
      response.TrySetException(new VouchaApiException(HttpStatusCode.Unauthorized, null));
      await create;
    }

    Assert.Null(viewModel.ApiKeySecret);
    var newOwnerKey = viewModel.ApiKeys.Single() with { UserId = "user-2" };
    service.RotateApiKeyOverride = _ => Task.FromResult(new ApiKeyCreationResponse(newOwnerKey, "raw-user-2"));
    await viewModel.RotateApiKeyAsync(newOwnerKey, cancellation.Token);
    Assert.Equal(1, service.RotateApiKeyCount);
    Assert.Equal("raw-user-2", viewModel.ApiKeySecret);
  }

  [Fact]
  public async Task HeldCreateMustFinishAndBeDismissedBeforeRotation()
  {
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    var service = new RecordingSettingsService();
    using var viewModel = new SettingsViewModel(service) { ApiKeyLabel = "Reader", ApiKeyType = "mcp" };
    await viewModel.LoadAsync(cancellation.Token);
    viewModel.SetApiKeyScopeSelected("mcp.user:write", true);
    var key = viewModel.ApiKeys.Single();
    var started = NewSignal();
    var createResponse = NewSource<ApiKeyCreationResponse>();
    service.CreateApiKeyOverride = () =>
    {
      started.TrySetResult(true);
      return createResponse.Task;
    };

    var create = viewModel.CreateApiKeyAsync(cancellation.Token);
    try
    {
      await started.Task.WaitAsync(cancellation.Token);
      await viewModel.RotateApiKeyAsync(key, cancellation.Token);
      Assert.Equal(0, service.RotateApiKeyCount);
      Assert.Null(viewModel.ApiKeySecret);
    }
    finally
    {
      createResponse.TrySetResult(new ApiKeyCreationResponse(key, "older-created-raw"));
      await create;
    }

    Assert.Equal("older-created-raw", viewModel.ApiKeySecret);
    await viewModel.RotateApiKeyAsync(key, cancellation.Token);
    Assert.Equal(0, service.RotateApiKeyCount);
    viewModel.DismissApiKeySecret();
    await viewModel.RotateApiKeyAsync(key, cancellation.Token);
    Assert.Equal("replacement-raw", viewModel.ApiKeySecret);
    Assert.NotNull(viewModel.ApiKeyRotationNotice);
  }

  [Fact]
  public async Task SameOwnerRefreshKeepsRotationBusyUntilTheHeldOperationExits()
  {
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    var service = new RecordingSettingsService();
    using var viewModel = new SettingsViewModel(service);
    await viewModel.LoadAsync(cancellation.Token);
    var key = viewModel.ApiKeys.Single();
    var started = NewSignal();
    var response = NewSource<ApiKeyCreationResponse>();
    service.RotateApiKeyOverride = _ => service.RotateApiKeyCount == 1
        ? HoldRotation(started, response)
        : Task.FromResult(RotatedResponse());
    var identityStarted = NewSignal();
    var identityResponse = NewSource<MyIdentityResponse>();
    service.FetchMyIdentityOverride = count => count == 2
        ? Hold(identityStarted, identityResponse)
        : Task.FromResult(RecordingSettingsService.CreateIdentity());

    var first = viewModel.RotateApiKeyAsync(key, cancellation.Token);
    try
    {
      await started.Task.WaitAsync(cancellation.Token);
      var refresh = viewModel.LoadAsync(cancellation.Token);
      try
      {
        await identityStarted.Task.WaitAsync(cancellation.Token);
        await viewModel.RotateApiKeyAsync(key, cancellation.Token);
        Assert.Equal(1, service.RotateApiKeyCount);
      }
      finally
      {
        identityResponse.TrySetResult(RecordingSettingsService.CreateIdentity());
        await refresh;
      }

      await viewModel.RotateApiKeyAsync(key, cancellation.Token);
      Assert.Equal(1, service.RotateApiKeyCount);
    }
    finally
    {
      response.TrySetResult(RotatedResponse());
      await first;
    }

    await viewModel.RotateApiKeyAsync(key, cancellation.Token);
    Assert.Equal(1, service.RotateApiKeyCount);
    Assert.Equal("replacement-raw", viewModel.ApiKeySecret);
    viewModel.DismissApiKeySecret();
    await viewModel.RotateApiKeyAsync(key, cancellation.Token);
    Assert.Equal(2, service.RotateApiKeyCount);
  }

  [Fact]
  public async Task StalePriorOwnerCompletionCannotReleaseNewSameIdRotation()
  {
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    var service = new RecordingSettingsService();
    using var viewModel = new SettingsViewModel(service);
    await viewModel.LoadAsync(cancellation.Token);
    var key = viewModel.ApiKeys.Single();
    service.FetchMyIdentityOverride = count => Task.FromResult(
        RecordingSettingsService.CreateIdentity(count == 2 ? "user-2" : "user-1"));
    service.FetchApiKeysOverride = count => Task.FromResult(new ApiKeyListResponse(
        [key with { UserId = count == 2 ? "user-2" : "user-1" }], new PageInfo(null, false, null)));
    var held = new[] { NewSource<ApiKeyCreationResponse>(), NewSource<ApiKeyCreationResponse>(),
        NewSource<ApiKeyCreationResponse>() };
    service.RotateApiKeyOverride = _ => held[service.RotateApiKeyCount - 1].Task;

    var oldA = viewModel.RotateApiKeyAsync(key, cancellation.Token);
    Task? oldB = null;
    Task? currentA = null;
    try
    {
      await viewModel.LoadAsync(cancellation.Token);
      oldB = viewModel.RotateApiKeyAsync(viewModel.ApiKeys.Single(), cancellation.Token);
      await viewModel.LoadAsync(cancellation.Token);
      currentA = viewModel.RotateApiKeyAsync(viewModel.ApiKeys.Single(), cancellation.Token);
      Assert.Equal(3, service.RotateApiKeyCount);

      held[0].TrySetResult(RotatedResponse());
      await oldA;
      await viewModel.RotateApiKeyAsync(viewModel.ApiKeys.Single(), cancellation.Token);
      Assert.Equal(3, service.RotateApiKeyCount);
    }
    finally
    {
      foreach (var response in held) response.TrySetResult(RotatedResponse());
      await oldA;
      if (oldB is not null) await oldB;
      if (currentA is not null) await currentA;
    }
  }
}
