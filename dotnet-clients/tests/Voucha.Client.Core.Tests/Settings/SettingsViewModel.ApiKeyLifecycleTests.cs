using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelActionsTests
{
  [Fact]
  public async Task LifetimeChoicesFollowCurrentOwnerRoleAndCreationForwardsExplicitChoice()
  {
    var ordinaryService = new RecordingSettingsService();
    var ordinary = new SettingsViewModel(ordinaryService) { ApiKeyLabel = "Reader" };
    await ordinary.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Equal("90", ordinary.SelectedApiKeyLifetimeOption.ProtocolValue);
    Assert.Equal(["30", "90", "365", "none"], ordinary.ApiKeyLifetimeOptions.Select(option => option.ProtocolValue));
    ordinary.SelectedApiKeyLifetimeOption = ordinary.ApiKeyLifetimeOptions.Single(option => option.ProtocolValue == "none");
    ordinary.SetApiKeyScopeSelected("rss:read", true);
    await ordinary.CreateApiKeyAsync(TestContext.Current.CancellationToken);
    Assert.Null(ordinaryService.LastCreatedApiKeyLifetimeDays);

    var administratorService = new RecordingSettingsService { UserRoles = ["administrator"] };
    var administrator = new SettingsViewModel(administratorService) { ApiKeyLabel = "Admin" };
    await administrator.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Equal("30", administrator.SelectedApiKeyLifetimeOption.ProtocolValue);
    Assert.Equal(["30", "90"], administrator.ApiKeyLifetimeOptions.Select(option => option.ProtocolValue));
    administrator.SelectedApiKeyLifetimeOption = new UiProtocolOption("none", UiText.Verbatim("No expiry"), UiLocalization.English);
    Assert.Equal(30, administrator.ApiKeyLifetimeDays);
    administrator.SetApiKeyScopeSelected("rss:read", true);
    await administrator.CreateApiKeyAsync(TestContext.Current.CancellationToken);
    Assert.Equal(30, administratorService.LastCreatedApiKeyLifetimeDays);
  }

  [Fact]
  public async Task RotationShowsReplacementOnceAndReportsMissingOrInactiveKeysTruthfully()
  {
    var service = new RecordingSettingsService();
    var model = new SettingsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var original = Assert.Single(model.ApiKeys);

    service.RotateStatus = HttpStatusCode.NotFound;
    await model.RotateApiKeyAsync(original, TestContext.Current.CancellationToken);
    Assert.Null(model.ApiKeySecret);
    Assert.Contains("no longer available", model.ApiKeyRotationNotice, StringComparison.OrdinalIgnoreCase);
    Assert.Equal(original.Id, Assert.Single(model.ApiKeys).Id);

    service.RotateStatus = HttpStatusCode.Conflict;
    await model.RotateApiKeyAsync(original, TestContext.Current.CancellationToken);
    Assert.Null(model.ApiKeySecret);
    Assert.Contains("cannot be rotated", model.ApiKeyRotationNotice, StringComparison.OrdinalIgnoreCase);
    Assert.Equal(original.Id, Assert.Single(model.ApiKeys).Id);

    service.RotateStatus = null;
    await model.RotateApiKeyAsync(original, TestContext.Current.CancellationToken);
    Assert.Equal(original.Id, service.LastRotatedApiKeyId);
    Assert.Equal("replacement", Assert.Single(model.ApiKeys).Id);
    Assert.Equal("replacement-raw", model.ApiKeySecret);
    model.DismissApiKeySecret();
    Assert.Null(model.ApiKeySecret);
    Assert.False(model.HasApiKeySecret);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Null(model.ApiKeySecret);
  }

  [Fact]
  public async Task RenderRowsDistinguishInvalidAdministratorExpiredAndReplacedKeys()
  {
    var service = new RecordingSettingsService { UserRoles = ["administrator"] };
    var model = new SettingsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var row = Assert.Single(model.LocalizedApiKeys);
    Assert.True(row.IsAdministratorInvalid);
    Assert.Contains("invalid", row.LocalizedStatus, StringComparison.OrdinalIgnoreCase);
    Assert.True(row.CanRotate);
    var expired = row with { ProtocolValue = row.ProtocolValue with { ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1) } };
    Assert.Contains("Expired", expired.LocalizedStatus, StringComparison.OrdinalIgnoreCase);
    Assert.False(expired.CanRotate);
    var replaced = row with { ProtocolValue = row.ProtocolValue with { ReplacedByApiKeyId = "new-key" } };
    Assert.Contains("Replaced", replaced.LocalizedStatus, StringComparison.OrdinalIgnoreCase);
    Assert.False(replaced.CanRotate);
  }

  [Fact]
  public async Task LateRotationListCannotReplaceNewerSettingsLoad()
  {
    var heldPage = new TaskCompletionSource<ApiKeyListResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var pageStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new RecordingSettingsService();
    var model = new SettingsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var original = Assert.Single(model.ApiKeys);
    var fresh = original with { Id = "fresh-key" };
    service.FetchApiKeysOverride = count =>
    {
      if (count == 2)
      {
        pageStarted.TrySetResult();
        return heldPage.Task;
      }
      return Task.FromResult(new ApiKeyListResponse([fresh], new PageInfo(null, false, null)));
    };

    var rotation = model.RotateApiKeyAsync(original, TestContext.Current.CancellationToken);
    try
    {
      await pageStarted.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
      await model.LoadAsync(TestContext.Current.CancellationToken);
      Assert.Equal("fresh-key", Assert.Single(model.ApiKeys).Id);
    }
    finally
    {
      heldPage.TrySetResult(new ApiKeyListResponse([original], new PageInfo(null, false, null)));
      await rotation;
    }
    Assert.Equal("fresh-key", Assert.Single(model.ApiKeys).Id);
    Assert.Null(model.ApiKeySecret);
    Assert.Null(model.ApiKeyRotationNotice);
  }

  [Fact]
  public async Task LateRotationPostPreservesSecretAfterSameOwnerReload()
  {
    var heldPost = new TaskCompletionSource<ApiKeyCreationResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var postStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new RecordingSettingsService();
    var model = new SettingsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var original = Assert.Single(model.ApiKeys);
    service.RotateApiKeyOverride = _ =>
    {
      postStarted.TrySetResult();
      return heldPost.Task;
    };
    var rotation = model.RotateApiKeyAsync(original, TestContext.Current.CancellationToken);
    try
    {
      await postStarted.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
      await model.LoadAsync(TestContext.Current.CancellationToken);
    }
    finally
    {
      heldPost.TrySetResult(new ApiKeyCreationResponse(original with { Id = "old-owner-replacement" }, "old-owner-secret"));
      await rotation;
    }
    Assert.Equal("old-owner-secret", model.ApiKeySecret);
    Assert.NotNull(model.ApiKeyRotationNotice);
    Assert.Equal(original.Id, Assert.Single(model.ApiKeys).Id);
  }
}
