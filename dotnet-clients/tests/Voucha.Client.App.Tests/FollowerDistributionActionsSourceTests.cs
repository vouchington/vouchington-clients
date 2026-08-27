using System.Runtime.CompilerServices;
using System.Globalization;
using Voucha.Client.App.Pages;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class FollowerDistributionActionsSourceTests
{
  [Fact]
  public void ExposesShareAndSendPickerWithServerSearchAndPagination()
  {
    var actions = Source("FollowerDistributionActions.cs");
    var picker = Source("FollowerDistributionSendPage.cs");
    Assert.Contains("NativeSwiftFollowerDistributionShareWithFollowers", actions, StringComparison.Ordinal);
    Assert.Contains("NativeSwiftFollowerDistributionSendToFollowers", actions, StringComparison.Ordinal);
    Assert.Contains("SearchFollowersAsync", picker, StringComparison.Ordinal);
    Assert.Contains("LoadMoreFollowersAsync", picker, StringComparison.Ordinal);
    Assert.Contains("ToggleSelection", picker, StringComparison.Ordinal);
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
    var page = Source("RssFeedItemDetailPage.cs");

    Assert.Contains("sessionStore.SessionChanged += sessionChangedHandler", page, StringComparison.Ordinal);
    Assert.Contains("sessionStore.SessionChanged -= sessionChangedHandler", page, StringComparison.Ordinal);
    Assert.Contains("MainThread.BeginInvokeOnMainThread(RefreshFollowerSendVisibility)", page, StringComparison.Ordinal);
    Assert.Contains("followerSend.IsVisible =", page, StringComparison.Ordinal);
    Assert.Contains("FollowerDistributionActions.CanSendRssItem(sessionStore)", page, StringComparison.Ordinal);
  }

  [Fact]
  public void SendModalProvidesLocalizedCancelThatDismissesWithoutSubmitting()
  {
    var page = Source("FollowerDistributionSendPage.cs");

    Assert.Contains("AutomationId = \"follower-distribution-cancel\"", page, StringComparison.Ordinal);
    Assert.Contains("UiCopy.Localize(UiMessageKey.CommonCancel)", page, StringComparison.Ordinal);
    Assert.Contains("cancel.Clicked += OnCancelClicked", page, StringComparison.Ordinal);
    Assert.Contains("await Navigation.PopModalAsync()", page, StringComparison.Ordinal);
  }

  [Fact]
  public void SendModalKeepsSelectedRecipientsVisibleAndRemovableAcrossSearches()
  {
    var page = Source("FollowerDistributionSendPage.cs");

    Assert.Contains("state.SelectedRecipients", page, StringComparison.Ordinal);
    Assert.Contains("selectedRecipients.Children.Clear()", page, StringComparison.Ordinal);
    Assert.Contains("SelectedRecipient(user)", page, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeSwiftCommonRemove", page, StringComparison.Ordinal);
    Assert.Contains("state.ToggleSelection(user.Id); Refresh()", page, StringComparison.Ordinal);
  }

  [Fact]
  public void SendModalDismissesOnSessionChangesAndShowsSelectedAudienceBeforeSearching()
  {
    var actions = Source("FollowerDistributionActions.cs");
    var page = Source("FollowerDistributionSendPage.cs");

    Assert.Contains("sessionStore.Current.Identity?.Id == userId", actions, StringComparison.Ordinal);
    Assert.Contains("new FollowerDistributionSendPage(state, userId, sessionStore)", actions, StringComparison.Ordinal);
    Assert.Contains("sessionStore.SessionChanged += sessionChangedHandler", page, StringComparison.Ordinal);
    Assert.Contains("sessionStore.SessionChanged -= sessionChangedHandler", page, StringComparison.Ordinal);
    Assert.Contains("args.Snapshot.Identity?.Id != userId", page, StringComparison.Ordinal);
    Assert.Contains("MainThread.BeginInvokeOnMainThread(() => _ = DismissAsync())", page, StringComparison.Ordinal);
    Assert.Contains("state.SetAudience(true);\n      Refresh();\n      await state.SearchFollowersAsync", page, StringComparison.Ordinal);
  }

  [Fact]
  public void ActionsRevalidateTheViewerBeforeSharingAndUseGuardedDismissal()
  {
    var actions = Source("FollowerDistributionActions.cs");
    var page = Source("FollowerDistributionSendPage.cs");

    Assert.Contains("choice == share && sessionStore.Current.IsAuthenticated && sessionStore.Current.Identity?.Id == userId", actions, StringComparison.Ordinal);
    Assert.Contains("if (await state.SubmitAsync()) { await DismissAsync(); return; }", page, StringComparison.Ordinal);
  }

  private static string Source(string file, [CallerFilePath] string sourceFile = "")
  {
    var root = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!);
    while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "src"))) root = root.Parent;
    return File.ReadAllText(Path.Combine(root!.FullName, "src", "Voucha.Client.App", "Pages", file));
  }
}
