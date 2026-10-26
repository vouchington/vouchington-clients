using System.Globalization;
using Voucha.Client.App.Pages;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class FollowerDistributionActionsSourceTests
{
  [Fact]
  public void ExposesShareAndSendPickerWithServerSearchAndPagination()
  {
    var actions = AppSource.Read("Pages", "FollowerDistributionActions.cs");
    var picker = AppSource.Read("Pages", "FollowerDistributionSendPage.cs");
    var rows = AppSource.Read("Pages", "FollowerDistributionSendPage.Avatars.cs");
    Assert.Contains("NativeSwiftFollowerDistributionShareWithFollowers", actions, StringComparison.Ordinal);
    Assert.Contains("NativeSwiftFollowerDistributionSendToFollowers", actions, StringComparison.Ordinal);
    Assert.Contains("SearchFollowersAsync", picker, StringComparison.Ordinal);
    Assert.Contains("LoadMoreFollowersAsync", picker, StringComparison.Ordinal);
    Assert.Contains("ToggleSelection", rows, StringComparison.Ordinal);
    Assert.Contains("SubmitAsync", picker, StringComparison.Ordinal);
  }

  [Fact]
  public void VisibilityAllowsMissingCreatorForSignedInViewer()
  {
    var converter = new FollowerDistributionVisibilityConverter();
    Assert.True((bool)converter.Convert([true, true, null, "viewer"], typeof(bool), null, CultureInfo.InvariantCulture));
    Assert.False((bool)converter.Convert([true, true, "viewer", "viewer"], typeof(bool), null, CultureInfo.InvariantCulture));
  }

  [Fact]
  public void RssDetailRefreshesFollowerActionVisibilityWhenTheSessionChanges()
  {
    var page = AppSource.Read("Pages", "RssFeedItemDetailPage.cs");

    Assert.Contains("sessionStore.SessionChanged += sessionChangedHandler", page, StringComparison.Ordinal);
    Assert.Contains("sessionStore.SessionChanged -= sessionChangedHandler", page, StringComparison.Ordinal);
    Assert.Contains("MainThread.BeginInvokeOnMainThread(RefreshFollowerSendVisibility)", page, StringComparison.Ordinal);
    Assert.Contains("followerSend.IsVisible =", page, StringComparison.Ordinal);
    Assert.Contains("FollowerDistributionActions.CanSendRssItem(sessionStore)", page, StringComparison.Ordinal);
  }

  [Fact]
  public void SendModalProvidesLocalizedCancelThatDismissesWithoutSubmitting()
  {
    var page = AppSource.Read("Pages", "FollowerDistributionSendPage.cs");

    Assert.Contains("AutomationId = \"follower-distribution-cancel\"", page, StringComparison.Ordinal);
    Assert.Contains("UiCopy.Localize(UiMessageKey.CommonCancel)", page, StringComparison.Ordinal);
    Assert.Contains("cancel.Clicked += OnCancelClicked", page, StringComparison.Ordinal);
    Assert.Contains("await Navigation.PopModalAsync()", page, StringComparison.Ordinal);
  }

  [Fact]
  public void SendModalKeepsSelectedRecipientsVisibleAndRemovableAcrossSearches()
  {
    var page = AppSource.Read("Pages", "FollowerDistributionSendPage.cs");
    var rows = AppSource.Read("Pages", "FollowerDistributionSendPage.Avatars.cs");

    Assert.Contains("state.SelectedRecipients", page, StringComparison.Ordinal);
    Assert.Contains("RefreshRows(selectedRecipients, state.SelectedRecipients", page, StringComparison.Ordinal);
    Assert.Contains("RefreshRows(recipients, state.Followers", page, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeSwiftCommonRemove", rows, StringComparison.Ordinal);
    Assert.Contains("state.ToggleSelection(user.Id); Refresh()", rows, StringComparison.Ordinal);
  }

  [Fact]
  public void SendModalDismissesOnSessionChangesAndShowsSelectedAudienceBeforeSearching()
  {
    var actions = AppSource.Read("Pages", "FollowerDistributionActions.cs");
    var page = AppSource.Read("Pages", "FollowerDistributionSendPage.cs");

    Assert.Contains("sessionStore.Current.Identity?.Id == userId", actions, StringComparison.Ordinal);
    Assert.Contains("new FollowerDistributionSendPage(", actions, StringComparison.Ordinal);
    Assert.Contains("services.GetRequiredService<AppConfig>()", actions, StringComparison.Ordinal);
    Assert.Contains("sessionStore.SessionChanged += sessionChangedHandler", page, StringComparison.Ordinal);
    Assert.Contains("sessionStore.SessionChanged -= sessionChangedHandler", page, StringComparison.Ordinal);
    Assert.Contains("args.Snapshot.Identity?.Id != userId", page, StringComparison.Ordinal);
    Assert.Contains("MainThread.BeginInvokeOnMainThread(() => _ = DismissAsync())", page, StringComparison.Ordinal);
    Assert.Contains("state.SetAudience(true);\n      Refresh();\n      await state.SearchFollowersAsync", page, StringComparison.Ordinal);
  }

  [Fact]
  public void ActionsRevalidateTheViewerBeforeSharingAndUseGuardedDismissal()
  {
    var actions = AppSource.Read("Pages", "FollowerDistributionActions.cs");
    var page = AppSource.Read("Pages", "FollowerDistributionSendPage.cs");

    Assert.Contains("choice == share && sessionStore.Current.IsAuthenticated && sessionStore.Current.Identity?.Id == userId", actions, StringComparison.Ordinal);
    Assert.Contains("if (await state.SubmitAsync()) { await DismissAsync(); return; }", page, StringComparison.Ordinal);
  }
}
