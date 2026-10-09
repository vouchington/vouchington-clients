using System.Net;
using System.Net.Http;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelActionsTests
{
  [Fact]
  public async Task RotationSecretWaitsForLatestSameOwnerAcrossOverlappingAndTransientLoads()
  {
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    var service = new RecordingSettingsService();
    using var viewModel = new SettingsViewModel(service);
    await viewModel.LoadAsync(cancellation.Token);

    var rotationStarted = NewSignal();
    var rotationResponse = NewSource<ApiKeyCreationResponse>();
    service.RotateApiKeyOverride = _ =>
    {
      rotationStarted.TrySetResult(true);
      return rotationResponse.Task;
    };

    var firstIdentityStarted = NewSignal();
    var firstIdentity = NewSource<MyIdentityResponse>();
    var secondIdentityStarted = NewSignal();
    var secondIdentity = NewSource<MyIdentityResponse>();
    var retryIdentityStarted = NewSignal();
    var retryIdentity = NewSource<MyIdentityResponse>();
    service.FetchMyIdentityOverride = count => count switch
    {
      2 => Hold(firstIdentityStarted, firstIdentity),
      3 => Hold(secondIdentityStarted, secondIdentity),
      4 => Hold(retryIdentityStarted, retryIdentity),
      _ => Task.FromResult(RecordingSettingsService.CreateIdentity()),
    };

    var rotation = viewModel.RotateApiKeyAsync(viewModel.ApiKeys.Single(), cancellation.Token);
    await rotationStarted.Task.WaitAsync(cancellation.Token);
    var staleLoad = viewModel.LoadAsync(cancellation.Token);
    await firstIdentityStarted.Task.WaitAsync(cancellation.Token);
    var latestLoad = viewModel.LoadAsync(cancellation.Token);
    await secondIdentityStarted.Task.WaitAsync(cancellation.Token);

    firstIdentity.TrySetResult(RecordingSettingsService.CreateIdentity());
    await staleLoad;
    rotationResponse.TrySetResult(RotatedResponse());
    await rotation;
    Assert.Null(viewModel.ApiKeySecret);

    secondIdentity.TrySetException(new HttpRequestException("temporary identity outage"));
    await latestLoad;
    Assert.Null(viewModel.ApiKeySecret);

    var retryLoad = viewModel.LoadAsync(cancellation.Token);
    await retryIdentityStarted.Task.WaitAsync(cancellation.Token);
    retryIdentity.TrySetResult(RecordingSettingsService.CreateIdentity());
    await retryLoad;

    Assert.Equal("replacement-raw", viewModel.ApiKeySecret);
    Assert.NotNull(viewModel.ApiKeyRotationNotice);
  }

  [Fact]
  public async Task RotationSecretIsDiscardedWhenRefreshConfirmsAnotherOwner()
  {
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    var service = new RecordingSettingsService();
    using var viewModel = new SettingsViewModel(service);
    await viewModel.LoadAsync(cancellation.Token);

    var rotationStarted = NewSignal();
    var rotationResponse = NewSource<ApiKeyCreationResponse>();
    service.RotateApiKeyOverride = _ =>
    {
      rotationStarted.TrySetResult(true);
      return rotationResponse.Task;
    };
    var identityStarted = NewSignal();
    var identity = NewSource<MyIdentityResponse>();
    service.FetchMyIdentityOverride = count => count == 2
        ? Hold(identityStarted, identity)
        : Task.FromResult(RecordingSettingsService.CreateIdentity());

    var rotation = viewModel.RotateApiKeyAsync(viewModel.ApiKeys.Single(), cancellation.Token);
    await rotationStarted.Task.WaitAsync(cancellation.Token);
    var load = viewModel.LoadAsync(cancellation.Token);
    await identityStarted.Task.WaitAsync(cancellation.Token);
    rotationResponse.TrySetResult(RotatedResponse());
    await rotation;
    Assert.Null(viewModel.ApiKeySecret);

    identity.TrySetResult(RecordingSettingsService.CreateIdentity("user-2"));
    await load;

    Assert.Equal("user-2", service.LastFetchedUserId);
    Assert.Null(viewModel.ApiKeySecret);
    Assert.Null(viewModel.ApiKeySecretDisplay);
  }

  [Fact]
  public async Task RotationSecretIsDiscardedWhenRefreshConfirmsSignedOutSession()
  {
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    var service = new RecordingSettingsService();
    using var viewModel = new SettingsViewModel(service);
    await viewModel.LoadAsync(cancellation.Token);

    var rotationStarted = NewSignal();
    var rotationResponse = NewSource<ApiKeyCreationResponse>();
    service.RotateApiKeyOverride = _ =>
    {
      rotationStarted.TrySetResult(true);
      return rotationResponse.Task;
    };
    var identityStarted = NewSignal();
    var identity = NewSource<MyIdentityResponse>();
    service.FetchMyIdentityOverride = count =>
    {
      if (count != 2) return Task.FromResult(RecordingSettingsService.CreateIdentity());
      identityStarted.TrySetResult(true);
      return identity.Task;
    };

    var rotation = viewModel.RotateApiKeyAsync(viewModel.ApiKeys.Single(), cancellation.Token);
    await rotationStarted.Task.WaitAsync(cancellation.Token);
    var load = viewModel.LoadAsync(cancellation.Token);
    await identityStarted.Task.WaitAsync(cancellation.Token);
    rotationResponse.TrySetResult(RotatedResponse());
    await rotation;
    Assert.Null(viewModel.ApiKeySecret);

    identity.TrySetException(new VouchaApiException(HttpStatusCode.Unauthorized, null));
    await load;

    Assert.Null(viewModel.ApiKeySecret);
    Assert.Null(viewModel.ApiKeySecretDisplay);
  }

  [Theory]
  [InlineData("current-session")]
  [InlineData("all-sessions")]
  [InlineData("account-logout")]
  [InlineData("dispose")]
  public async Task LateRotationCannotRestoreSecretAfterTerminalOwnerInvalidation(string transition)
  {
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    var service = new RecordingSettingsService();
    var viewModel = new SettingsViewModel(service);
    await viewModel.LoadAsync(cancellation.Token);
    var apiKey = viewModel.ApiKeys.Single();

    var rotationStarted = NewSignal();
    var rotationResponse = NewSource<ApiKeyCreationResponse>();
    service.RotateApiKeyOverride = _ =>
    {
      rotationStarted.TrySetResult(true);
      return rotationResponse.Task;
    };

    var rotation = viewModel.RotateApiKeyAsync(apiKey, cancellation.Token);
    await rotationStarted.Task.WaitAsync(cancellation.Token);
    switch (transition)
    {
      case "current-session":
        Assert.True(await viewModel.RevokeSessionAsync(
            CreateSession(), cancellation.Token));
        break;
      case "all-sessions":
        Assert.True(await viewModel.RevokeAllSessionsAsync(cancellation.Token));
        break;
      case "account-logout":
        viewModel.DeleteConfirmation = "delete my account";
        Assert.True(await viewModel.DeleteAccountAsync(cancellation.Token));
        break;
      case "dispose":
        viewModel.Dispose();
        break;
      default:
        throw new ArgumentOutOfRangeException(nameof(transition));
    }

    rotationResponse.TrySetResult(RotatedResponse());
    await rotation;
    Assert.Null(viewModel.ApiKeySecret);
    Assert.Null(viewModel.ApiKeySecretDisplay);
    var dispatchedRotations = service.RotateApiKeyCount;
    await viewModel.RotateApiKeyAsync(apiKey, cancellation.Token);
    Assert.Equal(dispatchedRotations, service.RotateApiKeyCount);
    viewModel.Dispose();
  }

  [Fact]
  public async Task IdentityResponseStartedBeforeLogoutCannotReconfirmTheOldOwner()
  {
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    var service = new RecordingSettingsService();
    using var viewModel = new SettingsViewModel(service);
    await viewModel.LoadAsync(cancellation.Token);
    var apiKey = viewModel.ApiKeys.Single();
    var identityStarted = NewSignal();
    var identityResponse = NewSource<MyIdentityResponse>();
    service.FetchMyIdentityOverride = count => count == 2
        ? Hold(identityStarted, identityResponse)
        : Task.FromResult(RecordingSettingsService.CreateIdentity());

    var load = viewModel.LoadAsync(cancellation.Token);
    try
    {
      await identityStarted.Task.WaitAsync(cancellation.Token);
      Assert.True(await viewModel.RevokeAllSessionsAsync(cancellation.Token));
    }
    finally
    {
      identityResponse.TrySetResult(RecordingSettingsService.CreateIdentity());
      await load;
    }

    Assert.Null(viewModel.ApiKeySecret);
    var previousRotations = service.RotateApiKeyCount;
    await viewModel.RotateApiKeyAsync(apiKey, cancellation.Token);
    Assert.Equal(previousRotations, service.RotateApiKeyCount);
  }

  [Theory]
  [InlineData("current-session", true)]
  [InlineData("current-session", false)]
  [InlineData("all-sessions", true)]
  [InlineData("all-sessions", false)]
  [InlineData("account-logout", true)]
  [InlineData("account-logout", false)]
  public async Task StaleTerminalResponseCannotClearTheNewOwnersSecret(string transition, bool succeeded)
  {
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    var service = new RecordingSettingsService();
    using var viewModel = new SettingsViewModel(service);
    await viewModel.LoadAsync(cancellation.Token);
    var oldKey = viewModel.ApiKeys.Single();
    var newKey = oldKey with { Id = "user-2-key", UserId = "user-2" };
    var terminalStarted = NewSignal();
    var terminalResponse = NewSource<bool>();
    service.DeleteAuthSessionOverride = () => HoldTerminal(terminalStarted, terminalResponse);
    service.RevokeAuthSessionsOverride = () => HoldTerminal(terminalStarted, terminalResponse);
    service.DeleteUserOverride = async () =>
    {
      await HoldTerminal(terminalStarted, terminalResponse);
      return new DeleteUserResponse(true);
    };
    service.FetchMyIdentityOverride = count => Task.FromResult(
        RecordingSettingsService.CreateIdentity(count == 1 ? "user-1" : "user-2"));
    service.FetchApiKeysOverride = _ => Task.FromResult(
        new ApiKeyListResponse([newKey], new PageInfo(null, false, null)));
    service.RotateApiKeyOverride = _ => Task.FromResult(new ApiKeyCreationResponse(newKey, "raw-user-2"));

    Task<bool> staleTransition = transition switch
    {
      "current-session" => viewModel.RevokeSessionAsync(CreateSession(), cancellation.Token),
      "all-sessions" => viewModel.RevokeAllSessionsAsync(cancellation.Token),
      "account-logout" => DeleteAccountAfterConfirmation(),
      _ => throw new ArgumentOutOfRangeException(nameof(transition)),
    };
    async Task<bool> DeleteAccountAfterConfirmation()
    {
      viewModel.DeleteConfirmation = "delete my account";
      return await viewModel.DeleteAccountAsync(cancellation.Token);
    }

    try
    {
      await terminalStarted.Task.WaitAsync(cancellation.Token);
      await viewModel.LoadAsync(cancellation.Token);
      await viewModel.RotateApiKeyAsync(newKey, cancellation.Token);
      Assert.Equal("raw-user-2", viewModel.ApiKeySecret);
    }
    finally
    {
      if (succeeded) terminalResponse.TrySetResult(true);
      else terminalResponse.TrySetException(new VouchaApiException(HttpStatusCode.Unauthorized, null));
      Assert.False(await staleTransition);
    }
    Assert.Equal("raw-user-2", viewModel.ApiKeySecret);
  }

  [Fact]
  public async Task StaleRotationUnauthorizedCannotClearTheNewOwnersSecret()
  {
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    var service = new RecordingSettingsService();
    using var viewModel = new SettingsViewModel(service);
    await viewModel.LoadAsync(cancellation.Token);
    var oldKey = viewModel.ApiKeys.Single();
    var newKey = oldKey with { Id = "user-2-key", UserId = "user-2" };
    var oldRotationStarted = NewSignal();
    var oldRotationResponse = NewSource<ApiKeyCreationResponse>();
    service.RotateApiKeyOverride = id => id == oldKey.Id
        ? HoldRotation(oldRotationStarted, oldRotationResponse)
        : Task.FromResult(new ApiKeyCreationResponse(newKey, "raw-user-2"));
    service.FetchMyIdentityOverride = count => Task.FromResult(
        RecordingSettingsService.CreateIdentity(count == 1 ? "user-1" : "user-2"));

    var oldRotation = viewModel.RotateApiKeyAsync(oldKey, cancellation.Token);
    try
    {
      await oldRotationStarted.Task.WaitAsync(cancellation.Token);
      await viewModel.LoadAsync(cancellation.Token);
      await viewModel.RotateApiKeyAsync(newKey, cancellation.Token);
      Assert.Equal("raw-user-2", viewModel.ApiKeySecret);
    }
    finally
    {
      oldRotationResponse.TrySetException(new VouchaApiException(HttpStatusCode.Unauthorized, null));
      await oldRotation;
    }
    Assert.Equal("raw-user-2", viewModel.ApiKeySecret);
  }

  [Fact]
  public async Task StaleFollowupListUnauthorizedCannotClearTheNewOwnersSecret()
  {
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    var service = new RecordingSettingsService();
    using var viewModel = new SettingsViewModel(service);
    await viewModel.LoadAsync(cancellation.Token);
    var oldKey = viewModel.ApiKeys.Single();
    var newKey = oldKey with { Id = "user-2-key", UserId = "user-2" };
    var oldListStarted = NewSignal();
    var oldListResponse = NewSource<ApiKeyListResponse>();
    var heldFetchCount = service.FetchApiKeysCount + 1;
    service.FetchApiKeysOverride = count =>
    {
      if (count == heldFetchCount)
      {
        oldListStarted.TrySetResult(true);
        return oldListResponse.Task;
      }
      return Task.FromResult(new ApiKeyListResponse([newKey], new PageInfo(null, false, null)));
    };
    service.RotateApiKeyOverride = id => Task.FromResult(new ApiKeyCreationResponse(
        id == oldKey.Id ? oldKey : newKey,
        id == oldKey.Id ? "raw-user-1" : "raw-user-2"));
    service.FetchMyIdentityOverride = count => Task.FromResult(
        RecordingSettingsService.CreateIdentity(count == 1 ? "user-1" : "user-2"));

    var oldRotation = viewModel.RotateApiKeyAsync(oldKey, cancellation.Token);
    try
    {
      await oldListStarted.Task.WaitAsync(cancellation.Token);
      await viewModel.LoadAsync(cancellation.Token);
      await viewModel.RotateApiKeyAsync(newKey, cancellation.Token);
      Assert.Equal("raw-user-2", viewModel.ApiKeySecret);
    }
    finally
    {
      oldListResponse.TrySetException(new VouchaApiException(HttpStatusCode.Unauthorized, null));
      await oldRotation;
    }
    Assert.Equal("raw-user-2", viewModel.ApiKeySecret);
  }

  private static Task<ApiKeyCreationResponse> HoldRotation(
      TaskCompletionSource<bool> started, TaskCompletionSource<ApiKeyCreationResponse> response)
  {
    started.TrySetResult(true);
    return response.Task;
  }

  private static async Task HoldTerminal(
      TaskCompletionSource<bool> started, TaskCompletionSource<bool> response)
  {
    started.TrySetResult(true);
    await response.Task;
  }

  private static TaskCompletionSource<bool> NewSignal() =>
      new(TaskCreationOptions.RunContinuationsAsynchronously);

  private static TaskCompletionSource<T> NewSource<T>() =>
      new(TaskCreationOptions.RunContinuationsAsynchronously);

  private static Task<MyIdentityResponse> Hold(
      TaskCompletionSource<bool> started,
      TaskCompletionSource<MyIdentityResponse> result)
  {
    started.TrySetResult(true);
    return result.Task;
  }

  private static ApiKeyCreationResponse RotatedResponse() =>
      new(
          new ApiKey(
              "replacement",
              "user-1",
              "rk_replacement",
              "rss",
              "Reader",
              ["rss:read"],
              DateTimeOffset.UtcNow,
              null,
              null,
              DateTimeOffset.UtcNow),
          "replacement-raw");
}
