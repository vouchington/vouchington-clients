using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class CommunityAutomodFlagsViewTests
{
  [Fact]
  public void RenderedFlagActionsInvokeDismissAndNativePostCallbacks()
  {
    ConfigureResources();
    string? dismissedPost = null;
    string? openedPost = null;
    var view = new CommunityAutomodFlagsView(
        postId => { dismissedPost = postId; return Task.CompletedTask; },
        postId => { openedPost = postId; return Task.CompletedTask; });
    view.Update([Flag()], _ => false, null, null, false, false);

    Find<Button>(view, "community-automod-open:post-1").SendClicked();
    Find<Button>(view, "community-automod-dismiss:post-1").SendClicked();

    Assert.Equal("post-1", openedPost);
    Assert.Equal("post-1", dismissedPost);
    Assert.Contains(Descendants<Label>(view), label => label.Text == "Flagged post");
  }

  [Fact]
  public void PendingFlagDisablesDismissAndPaginationUsesTheRenderedButton()
  {
    ConfigureResources();
    var dismissCalls = 0;
    var pageCalls = 0;
    var view = new CommunityAutomodFlagsView(
        _ => { dismissCalls++; return Task.CompletedTask; },
        _ => Task.CompletedTask);
    view.LoadMoreRequested += (_, _) => pageCalls++;
    view.Update([Flag()], _ => true, "offline", null, true, false);

    var dismiss = Find<Button>(view, "community-automod-dismiss:post-1");
    Assert.False(dismiss.IsEnabled);
    Assert.Equal("offline", Find<Label>(view, "community-automod-flags-error").Text);
    Find<Button>(view, "community-automod-flags-more").SendClicked();

    Assert.Equal(0, dismissCalls);
    Assert.Equal(1, pageCalls);
  }

  [Theory]
  [InlineData(false, "/posts/post-1")]
  [InlineData(true, null)]
  public void UnavailablePostDisablesOnlyOpenAction(bool targetAvailable, string? targetPath)
  {
    ConfigureResources();
    var opened = 0;
    var dismissed = 0;
    var view = new CommunityAutomodFlagsView(
        _ => { dismissed++; return Task.CompletedTask; },
        _ => { opened++; return Task.CompletedTask; });
    view.Update([Flag() with { TargetAvailable = targetAvailable, TargetPath = targetPath }],
        _ => false, null, null, false, false);

    var open = Find<Button>(view, "community-automod-open:post-1");
    Assert.False(open.IsEnabled);
    open.SendClicked();
    Find<Button>(view, "community-automod-dismiss:post-1").SendClicked();

    Assert.Equal(0, opened);
    Assert.Equal(1, dismissed);
  }

  private static CommunityModerationQueueEntry Flag() =>
      new("flag-1", "community-1", "post", "post-1", null, "pending", "automod_flag", null, null, 0,
          DateTimeOffset.Parse("2026-07-01T00:00:00Z"), DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
          "Flagged post", "/posts/post-1", true, false, false, null, null, null, null, null,
          null, "/posts/post-1", null, null, false, null, null, null);

  private static T Find<T>(Element root, string automationId) where T : Element =>
      Assert.Single(Descendants<T>(root), item => item.AutomationId == automationId);

  private static IEnumerable<T> Descendants<T>(Element root) where T : Element
  {
    foreach (var child in ((IVisualTreeElement)root).GetVisualChildren().OfType<Element>())
    {
      if (child is T match) yield return match;
      foreach (var descendant in Descendants<T>(child)) yield return descendant;
    }
  }

  private static void ConfigureResources()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application { Resources =
    {
      [UiMessageKey.ExtractedCommunitiesCommunityAutomodFlagsPanelAutomodFlagsB5fc56db.Value] = "Automod flags",
      [UiMessageKey.ExtractedCommunitiesCommunityAutomodFlagsPanelPostsAutomodFlaggedForModeratorReview90005619.Value] = "Posts flagged for review",
      [UiMessageKey.ExtractedCommunitiesCommunityAutomodFlagsPanelDismiss48845bff.Value] = "Dismiss",
      [UiMessageKey.NativeDotnetCsharpCommunitiesOpen.Value] = "Open",
      [UiMessageKey.NativeDotnetDirectMessagesLoadMore.Value] = "Load more",
    } };
  }

  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  {
    public IDispatcher GetForCurrentThread() => ImmediateDispatcher.Instance;
  }

  private sealed class ImmediateDispatcher : IDispatcher
  {
    public static ImmediateDispatcher Instance { get; } = new();
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
  }
}
