using Voucha.Client.Core.Api;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelTests
{
  [Fact]
  public async Task SaveIdentityAsyncUsesTheIdentityUpdateBody()
  {
    var service = new FakeSettingsService();
    var viewModel = new SettingsViewModel(service)
    {
      Username = "new-alice",
      DisplayNameSource = "x",
      ProfileImageId = "image-2",
    };

    await viewModel.SaveIdentityAsync(TestContext.Current.CancellationToken);

    Assert.NotNull(service.LastIdentityUpdateBody);
    Assert.Equal("new-alice", service.LastIdentityUpdateBody!.Username);
    Assert.Equal("x", service.LastIdentityUpdateBody.UseDisplayNameFrom);
    Assert.Equal("image-2", service.LastIdentityUpdateBody.ProfileImageId?.Value);
    Assert.Equal("alice", viewModel.Username);
  }

  [Fact]
  public async Task UpdatePrivacySelectionAsyncPatchesOnlyTheChosenField()
  {
    var service = new FakeSettingsService();
    var viewModel = new SettingsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var row = viewModel.PrivacySelections.Single(item => item.Key == "likes_visibility");
    row.Value = "mutual_followers";

    await viewModel.UpdatePrivacySelectionAsync(row, TestContext.Current.CancellationToken);

    Assert.Equal("user-1", service.LastUpdatedUserIdOrSlug);
    Assert.NotNull(service.LastPrivacyUpdateBody);
    Assert.Equal("mutual_followers", service.LastPrivacyUpdateBody!.LikesVisibility);
    Assert.Null(service.LastPrivacyUpdateBody.FollowsVisibility);
    Assert.Null(service.LastPrivacyUpdateBody.DefaultPostPrivacy);
  }

  [Fact]
  public async Task UpdatePrivacySelectionAsyncUsesTheLocalePatchField()
  {
    var service = new FakeSettingsService();
    var viewModel = new SettingsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var row = viewModel.PrivacySelections.Single(item => item.Key == "ui_locale");
    row.Value = "fr";

    await viewModel.UpdatePrivacySelectionAsync(row, TestContext.Current.CancellationToken);

    Assert.Equal("user-1", service.LastUpdatedUserIdOrSlug);
    Assert.NotNull(service.LastPrivacyUpdateBody);
    Assert.Equal("fr", service.LastPrivacyUpdateBody!.UiLocale?.Value);
    Assert.Null(service.LastPrivacyUpdateBody.FollowsVisibility);
  }

  [Fact]
  public async Task DeleteAccountAsyncReturnsLogoutAndUpdatesSummary()
  {
    var service = new FakeSettingsService { DeleteUserResponse = new DeleteUserResponse(true) };
    var viewModel = new SettingsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    viewModel.DeleteConfirmation = "delete my account";

    var deleted = await viewModel.DeleteAccountAsync(TestContext.Current.CancellationToken);

    Assert.True(deleted);
    Assert.Equal("Account deleted", viewModel.IdentitySummary);
    Assert.Equal("user-1", service.LastDeletedUserIdOrSlug);
  }
}
