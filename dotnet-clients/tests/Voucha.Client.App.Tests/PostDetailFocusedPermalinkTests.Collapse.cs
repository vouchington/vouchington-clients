using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Controls;
using Voucha.Client.App.Pages;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Posts;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed partial class PostDetailFocusedPermalinkTests
{
  [Theory]
  [InlineData("deleted", "[deleted]")]
  [InlineData("unmoderated", "pending")]
  public async Task PlaceholderCommentAndAncestorSuppressAuthorBodyAndActions(string state, string label)
  {
    PrepareCollapseResources();
    var root = Post("root", null);
    var placeholder = Post("parent", root.Id) with
    {
      PlaceholderState = state,
      Title = "private title",
      Markdown = "private body",
      CreatedBy = new User("author", "private author"),
      CanDelete = true,
      CanLock = true,
    };
    var target = Post("target", placeholder.Id);
    var service = new ThreadService(new PostResponse(root), Response([placeholder, target]), Response([root, placeholder, target]));
    var binding = new PostDetailPageBinding(new CommentThreadViewModel(service, root.Id, "author"), new SessionStore(new(new User("author", "author"))));
    await binding.LoadAsync(target.Id, TestContext.Current.CancellationToken);
    var row = Assert.Single(binding.CommentRows, row => row.Id == placeholder.Id);
    Assert.Equal(label, row.LocalizedNotice);
    Assert.Equal(label, Assert.Single(binding.AncestorRows, row => row.Id == placeholder.Id).LocalizedTitle);
    Assert.Empty(row.AuthorText);
    Assert.Empty(row.BodyText);
    Assert.Null(row.BodyHtml);
    Assert.False(row.HasAuthorImage);
    Assert.False(row.CanReply);
    Assert.False(row.CanEdit);
    Assert.False(row.CanDelete);
    Assert.False(row.CanReport);
    Assert.False(row.CanSave);
    Assert.False(row.CanCreateVote);
    Assert.False(row.CanClearVote);
    Assert.False(row.CanLock);
    var card = CommentCard(binding, row);
    Assert.False(Assert.Single(card.GetVisualTreeDescendants().OfType<NativeHtmlContentView>()).IsVisible);
    Assert.False(Assert.Single(card.GetVisualTreeDescendants().OfType<Button>(), button => button.AutomationId == "authored-comment-quote").IsVisible);
    Assert.DoesNotContain(card.GetVisualTreeDescendants().OfType<Label>(), value => value.IsVisible && value.Text is "private author" or "private body");
    Assert.Contains(card.GetVisualTreeDescendants().OfType<Label>(), value => value.IsVisible && value.Text == label);
  }

  [Fact]
  public async Task CollapsedServerRowUsesMetricCountWithNoLoadedChildrenAndRendersExpandControl()
  {
    PrepareCollapseResources();
    var root = Post("root", null);
    var hidden = Post("hidden", root.Id);
    var response = Response([hidden]) with
    {
      Bookmarks = new Dictionary<string, IReadOnlyDictionary<string, bool>> { [hidden.Id] = new Dictionary<string, bool> { ["hide"] = true } },
      PostsMetrics = new Dictionary<string, PostMetrics> { [hidden.Id] = new(Count: new(99, Children: 12)) },
    };
    var service = new ThreadService(new PostResponse(root), response, Response([]));
    var binding = new PostDetailPageBinding(new CommentThreadViewModel(service, root.Id, "viewer"), new SessionStore(new(new User("viewer", "viewer"))));
    await binding.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
    var row = Assert.Single(binding.CommentRows);
    Assert.True(row.IsCollapsed);
    Assert.True(row.HasChildren);
    Assert.Equal(12, row.DirectReplyCount);
    Assert.Equal("12", row.CollapsedReplyCountText);
    var card = CommentCard(binding, row);
    var button = Assert.Single(card.GetVisualTreeDescendants().OfType<Button>(), value => value.AutomationId == "comment-collapse");
    Assert.True(button.IsVisible);
    Assert.True(button.IsEnabled);
    Assert.Equal("▸", button.Text);
    var count = Assert.Single(card.GetVisualTreeDescendants().OfType<Label>(), value => value.AutomationId == "comment-collapsed-reply-count");
    Assert.True(count.IsVisible);
    Assert.Equal("12", count.Text);
  }

  [Fact]
  public async Task OnlySignedOutCollapseReadsAndWritesDeviceState()
  {
    var root = Post("root", null);
    var child = Post("child", root.Id);
    var service = new ThreadService(new PostResponse(root), Response([child]), Response([]));
    var store = new CollapseStore();
    var session = new SessionStore(SessionSnapshot.Anonymous);
    var model = new CommentThreadViewModel(service, root.Id);
    var binding = new PostDetailPageBinding(model, session, collapseStore: store);
    await binding.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
    await binding.ToggleCollapseAsync(child.Id, TestContext.Current.CancellationToken);
    Assert.Equal([child.Id], store.Saved);
    Assert.Equal("comments-collapsed:root", store.LastKey);
    var reads = store.Reads;
    session.SetCurrent(new(new User("viewer", "viewer")));
    await binding.RefreshAfterSessionChangedAsync(TestContext.Current.CancellationToken);
    Assert.Empty(model.CollapsedCommentIds);
    await binding.ToggleCollapseAsync(child.Id, TestContext.Current.CancellationToken);
    Assert.Equal(reads, store.Reads);
    Assert.Equal(1, store.Writes);
    session.SetCurrent(SessionSnapshot.Anonymous);
    await binding.RefreshAfterSessionChangedAsync(TestContext.Current.CancellationToken);
    Assert.Contains(child.Id, model.CollapsedCommentIds);
  }

  [Fact]
  public async Task AccountSwitchClearsPreviouslyRenderedRowsBeforeRefetchCompletes()
  {
    var root = Post("private-root", null);
    var service = new ThreadService(new PostResponse(root), Response([Post("private-comment", root.Id)]), Response([]));
    var session = new SessionStore(new(new User("first", "first")));
    var binding = new PostDetailPageBinding(new CommentThreadViewModel(service, root.Id, "first"), session);
    await binding.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
    Assert.NotNull(binding.RootRow);
    Assert.Single(binding.CommentRows);
    var next = new TaskCompletionSource<PostResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.RootHandler = () => next.Task;
    session.SetCurrent(new(new User("second", "second")));
    var refreshing = binding.RefreshAfterSessionChangedAsync(TestContext.Current.CancellationToken);
    Assert.Null(binding.RootRow);
    Assert.Empty(binding.CommentRows);
    Assert.True(binding.IsLoading);
    next.SetResult(new PostResponse(root));
    await refreshing;
    Assert.False(binding.IsLoading);
    Assert.Equal("second", binding.ViewModel.CurrentUserId);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("viewer")]
  public async Task ReturnedPlaceholderOffersSubtreeExpansionBeforeAnySubtreeRequest(string? viewer)
  {
    PrepareCollapseResources();
    var root = Post("root", null);
    var head = Post("head", root.Id) with { PlaceholderState = "deleted" };
    var response = Response([head]) with
    {
      PostsMetrics = new Dictionary<string, PostMetrics> { [head.Id] = new(Count: new(1, Children: 1)) },
    };
    var service = new ThreadService(new PostResponse(root), response, Response([]));
    var session = viewer is null ? new SessionStore(new(null)) : new SessionStore(new(new User(viewer, viewer)));
    var binding = new PostDetailPageBinding(new CommentThreadViewModel(service, root.Id, viewer), session);
    await binding.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
    var row = Assert.Single(binding.CommentRows);
    Assert.False(binding.RootRow!.HasMoreSubtree);
    var card = CommentCard(binding, row);
    var button = Assert.Single(card.GetVisualTreeDescendants().OfType<Button>(), value => value.AutomationId == "comment-subtree-next");
    Assert.True(button.IsVisible);
    Assert.True(button.IsEnabled);
    await binding.LoadMoreSubtreeAsync(head.Id, TestContext.Current.CancellationToken);
    Assert.False(Assert.Single(binding.CommentRows).HasMoreSubtree);
  }

  private sealed partial class ThreadService
  {
    public Func<Task<PostResponse>>? RootHandler { get; set; }
  }

  private static Border CommentCard(PostDetailPageBinding binding, PostDetailPageRow row)
  {
    var page = new PostDetailPage { BindingContext = binding };
    var collection = Assert.Single(page.GetVisualTreeDescendants().OfType<CollectionView>(), view => ReferenceEquals(view.ItemsSource, binding.CommentRows));
    var card = Assert.IsType<Border>(collection.ItemTemplate.CreateContent());
    card.BindingContext = row;
    return card;
  }

  private static void PrepareCollapseResources()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var app = new Application();
    foreach (var key in new[] { "Body", "Eyebrow", "Headline", "Metadata" }) app.Resources[key] = new Style(typeof(Label));
    app.Resources["ProfilePlacementImageSource96"] = new PlacementImageSourceConverter(new AppConfig(new Uri("https://api.test")), 96);
  }

  private sealed class CollapseStore : IAnonymousCommentCollapseStore
  {
    public string? LastKey { get; private set; }
    public string[] Saved { get; private set; } = [];
    public int Reads { get; private set; }
    public int Writes { get; private set; }
    public IReadOnlyList<string> Load(string key) { ++Reads; LastKey = key; return Saved; }
    public void Save(string key, IReadOnlySet<string> ids) { ++Writes; LastKey = key; Saved = ids.ToArray(); }
  }
}
