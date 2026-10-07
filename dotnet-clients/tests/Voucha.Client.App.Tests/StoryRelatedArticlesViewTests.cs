using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.NewsFeeds;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class StoryRelatedArticlesViewTests
{
  [Fact]
  public async Task RendersPreviewImmediatelyAndRetainsItThroughDelayFailureAndRetry()
  {
    ConfigureResources();
    var related = new StoryRelatedArticles("story", "primary", [Item("peer-3")], new("opaque", true, null), UiLocalization.English);
    var primary = Item("primary") with { StoryId = "story", StoryArticles = related };
    var service = new Service(primary);
    var model = new NewsFeedsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var view = new StoryRelatedArticlesView { PrimaryItem = primary, CanVote = true, CanRelate = true };
    Task? pending = null;
    view.LoadMoreRequested += (_, _) => pending = model.LoadMoreStoryArticlesAsync(primary, TestContext.Current.CancellationToken);
    var toggle = Find<Button>(view, "story-related-articles");
    var loadMore = Find<Button>(view, "story-load-more");
    Assert.Equal("1+ related articles", toggle.Text);
    toggle.SendClicked();
    Assert.True(related.IsExpanded);
    Assert.Contains(Descendants<Label>(view), label => label.Text == "peer-3");
    Assert.Equal(0, service.StoryRequests);
    Assert.Equal(180, Assert.Single(Descendants<Image>(view)).HeightRequest);

    loadMore.SendClicked();
    Assert.True(related.IsLoading);
    Assert.False(loadMore.IsEnabled);
    Assert.Contains(Descendants<Label>(view), label => label.Text == "peer-3");
    service.Completion.SetException(new HttpRequestException("Offline"));
    await Assert.IsAssignableFrom<Task>(pending);
    Assert.True(loadMore.IsEnabled);
    Assert.Equal(UiLocalization.English.Localize(UiMessageKey.NativeCommonRetry), loadMore.Text);
    Assert.Contains(Descendants<Label>(view), label => label.Text == "peer-3");

    service.Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    loadMore.SendClicked();
    service.Completion.SetResult(new([Item("peer-2")], new(null, false, null)));
    await Assert.IsAssignableFrom<Task>(pending);
    Assert.Equal(["peer-3", "peer-2"], related.Items.Select(item => item.Id));
    Assert.Equal("2 related articles", toggle.Text);
    Assert.False(loadMore.IsVisible);
    Assert.Contains(Descendants<Label>(view), label => label.Text == "peer-2");
  }

  [Fact]
  public void ArticleActionsForwardTheActualPeerWithoutReplacingItsPrimary()
  {
    ConfigureResources();
    var peer = Item("peer");
    var primary = Item("primary") with { StoryArticles = new("story", "primary", [peer], new(null, false, null), UiLocalization.English) };
    var view = new StoryRelatedArticlesView { PrimaryItem = primary, CanRelate = true };
    object? clicked = null;
    view.SaveRequested += (sender, _) => clicked = ((Button)sender!).CommandParameter;
    var save = Assert.Single(Descendants<Button>(view), button => button.Text == peer.SaveActionLabel);
    save.SendClicked();
    Assert.Same(peer, clicked);
    Assert.Same(primary, view.PrimaryItem);
  }

  private static void ConfigureResources()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application { Resources =
    {
      ["Headline"] = new Style(typeof(Label)), ["Body"] = new Style(typeof(Label)),
      ["Eyebrow"] = new Style(typeof(Label)), ["Metadata"] = new Style(typeof(Label)),
    } };
  }

  private static NewsFeedItem Item(string id) => new(id, id, "Source", "Summary", null, DateTimeOffset.UnixEpoch);
  private static T Find<T>(Element root, string id) where T : Element => Assert.Single(Descendants<T>(root), element => element.AutomationId == id);
  private static IEnumerable<T> Descendants<T>(Element root) where T : Element
  {
    foreach (var child in ((IVisualTreeElement)root).GetVisualChildren().OfType<Element>())
    {
      if (child is T match) yield return match;
      foreach (var match in Descendants<T>(child)) yield return match;
    }
  }

  private sealed class Service(NewsFeedItem primary) : INewsFeedService, IStoryRelatedArticlesService
  {
    public TaskCompletionSource<NewsFeedPage> Completion { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int StoryRequests { get; private set; }
    public Task<NewsFeedPage> GetStoryRelatedArticlesPageAsync(string storyId, string primaryItemId, string? after, CancellationToken cancellationToken = default)
    { StoryRequests++; return Completion.Task; }
    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(NewsFeedScope scope, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<NewsFeedItem>>([primary]);
    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(NewsFeedScope scope, NewsFeedSourceType sourceFeedType, CancellationToken cancellationToken = default) => GetNewsFeedItemsAsync(scope, cancellationToken);
    public Task SetSourceFollowAsync(string sourceId, bool following, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SetTopicFollowAsync(string topicId, bool following, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SetReadAsync(string itemId, bool read, CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  { public IDispatcher GetForCurrentThread() => ImmediateDispatcher.Instance; }
  private sealed class ImmediateDispatcher : IDispatcher
  {
    public static ImmediateDispatcher Instance { get; } = new();
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => new Timer();
  }
  private sealed class Timer : IDispatcherTimer
  {
    public TimeSpan Interval { get; set; }
    public bool IsRepeating { get; set; }
    public bool IsRunning { get; private set; }
    public event EventHandler? Tick;
    public void Start() { IsRunning = true; Tick?.Invoke(this, EventArgs.Empty); IsRunning = false; }
    public void Stop() => IsRunning = false;
  }
}
