using Voucha.Client.Core.Api;
using Voucha.Client.Core.Content;
using Voucha.Client.Core.Profiles;
using Xunit;

namespace Voucha.Client.Core.Tests.Profiles;

public sealed partial class ProfileViewModelSafetyTests
{
  [Fact]
  public async Task PostsHistoryUsesResponseEmbedSidecar()
  {
    var embed = new UrlEmbed(
        Title: "Embedded post",
        SourceUrl: "https://example.com/post",
        PlayerUrl: "https://www.youtube-nocookie.com/embed/example");
    var posts = new RecordingPostsService
    {
      FetchPostsAsyncOverride = (_, request) => Task.FromResult(new PostsFeedResponse(
          [Reference("post-1")],
          new PageInfo(null, false, null),
          new Dictionary<string, Post> { ["post-1"] = new("post-1", "review", "Post", "Body", request.Creator) },
          new Dictionary<string, User>(),
          new Dictionary<string, Community>(),
          PostLinkEmbeds: new Dictionary<string, UrlEmbed> { ["post-1"] = embed })),
    };
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"), posts: posts);

    await viewModel.LoadPublicScopeAsync("bob", NativeUserProfileScope.Overview, TestContext.Current.CancellationToken);

    var preview = Assert.IsType<UrlEmbedPreview>(Assert.Single(viewModel.HistoryItems).EmbedPreview);
    Assert.Equal("Embedded post", preview.Title);
    Assert.True(preview.CanPlay);
  }

  [Fact]
  public async Task PostsScopeLoadsSelectedTypeAndAppendsStableCursorPage()
  {
    var posts = new RecordingPostsService
    {
      FetchPostsAsyncOverride = (call, request) => Task.FromResult(PostsPage(call, request)),
    };
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"), posts: posts);
    var scope = new NativeUserProfileScope(
        NativeUserProfileSection.Posts,
        NativeUserProfileCollectionKind.Comments);

    await viewModel.LoadPublicScopeAsync("bob", scope, TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal("comment", posts.LastRequest?.PostTypes);
    Assert.Equal("cursor-1", posts.LastRequest?.After);
    Assert.Equal(["post-1", "post-2"], viewModel.HistoryItems.Select(row => row.Id));
    Assert.False(viewModel.CanLoadMore);
  }

  [Fact]
  public async Task PostsScopeAppendFailurePreservesPageAndAllowsSameCursorRetry()
  {
    var cursors = new List<string?>();
    var posts = new RecordingPostsService
    {
      FetchPostsAsyncOverride = (call, request) =>
      {
        cursors.Add(request.After);
        return call == 2
            ? Task.FromException<PostsFeedResponse>(new InvalidOperationException("Next page failed"))
            : Task.FromResult(PostsPage(call == 1 ? 1 : 2, request));
      },
    };
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"), posts: posts);
    var scope = new NativeUserProfileScope(
        NativeUserProfileSection.Posts,
        NativeUserProfileCollectionKind.Comments);

    await viewModel.LoadPublicScopeAsync("bob", scope, TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["post-1"], viewModel.HistoryItems.Select(row => row.Id));
    Assert.Equal("Next page failed", viewModel.CollectionErrorMessage);
    Assert.True(viewModel.CanLoadMore);
    Assert.False(viewModel.IsLoadingMore);

    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal([null, "cursor-1", "cursor-1"], cursors);
    Assert.Equal(["post-1", "post-2"], viewModel.HistoryItems.Select(row => row.Id));
    Assert.Null(viewModel.CollectionErrorMessage);
    Assert.False(viewModel.CanLoadMore);
  }

  [Fact]
  public async Task PostFilterFailurePreservesProfileAndRowsUntilRetryReplacesThem()
  {
    var afters = new List<string?>();
    var posts = new RecordingPostsService
    {
      FetchPostsAsyncOverride = (call, request) =>
      {
        afters.Add(request.After);
        return call switch
        {
          1 => Task.FromResult(SinglePostPage(
              "post-initial", "review", request, "cursor-old", true)),
          2 => Task.FromException<PostsFeedResponse>(new InvalidOperationException("Filter failed")),
          3 => Task.FromResult(SinglePostPage("post-replacement", "comment", request)),
          _ => throw new InvalidOperationException($"Unexpected posts request {call}"),
        };
      },
    };
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"), posts: posts);

    await viewModel.LoadPublicScopeAsync(
        "bob",
        new NativeUserProfileScope(
            NativeUserProfileSection.Posts,
            NativeUserProfileCollectionKind.PostsAll),
        TestContext.Current.CancellationToken);
    Assert.True(viewModel.CanLoadMore);
    await viewModel.SelectScopeAsync(
        NativeUserProfileCollectionKind.Comments,
        TestContext.Current.CancellationToken);

    Assert.Equal(NativeUserProfileCollectionKind.Comments, viewModel.ProfileScope.Collection);
    Assert.Equal(ProfileHistoryTab.Comments, viewModel.SelectedHistoryTab);
    Assert.Equal("bob", viewModel.User?.Username);
    Assert.Equal(["post-initial"], viewModel.HistoryItems.Select(row => row.Id));
    Assert.Equal("Filter failed", viewModel.CollectionErrorMessage);
    Assert.False(viewModel.CanLoadMore);

    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, posts.CallCount);

    await viewModel.SelectScopeAsync(
        NativeUserProfileCollectionKind.Comments,
        TestContext.Current.CancellationToken);

    Assert.Equal([null, null, null], afters);
    Assert.Equal(["post-replacement"], viewModel.HistoryItems.Select(row => row.Id));
    Assert.Null(viewModel.CollectionErrorMessage);
  }

  [Fact]
  public async Task OverviewPreservesEmbeddedAllHistory()
  {
    var posts = new RecordingPostsService();
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"), posts: posts);

    await viewModel.LoadPublicScopeAsync(
        "bob",
        NativeUserProfileScope.Overview,
        TestContext.Current.CancellationToken);

    Assert.True(viewModel.IsOverviewScope);
    Assert.True(viewModel.IsPostsScope);
    Assert.Equal("review,discussion,comment", posts.LastRequest?.PostTypes);
  }

  private static PostsFeedResponse PostsPage(int call, FetchPostsRequest request)
  {
    var first = new Post("post-1", "comment", "First", "Body", request.Creator);
    var second = new Post("post-2", "comment", "Second", "Body", request.Creator);
    var results = call == 1 ? new[] { Reference("post-1") } : [Reference("post-1"), Reference("post-2")];
    var posts = call == 1
        ? new Dictionary<string, Post> { [first.Id] = first }
        : new Dictionary<string, Post> { [first.Id] = first, [second.Id] = second };
    return new(
        results,
        new PageInfo(call == 1 ? "cursor-1" : null, call == 1, null),
        posts,
        new Dictionary<string, User>(),
        new Dictionary<string, Community>());
  }

  private static PostsFeedResponse SinglePostPage(
      string id,
      string postType,
      FetchPostsRequest request,
      string? endCursor = null,
      bool hasNextPage = false)
  {
    var post = new Post(id, postType, id, "Body", request.Creator);
    return new(
        [Reference(id)],
        new PageInfo(endCursor, hasNextPage, null),
        new Dictionary<string, Post> { [id] = post },
        new Dictionary<string, User>(),
        new Dictionary<string, Community>());
  }

  private static EntityReference Reference(string id) =>
      new(null, null, id, null, null, null, null, null, null, null, null);
}
