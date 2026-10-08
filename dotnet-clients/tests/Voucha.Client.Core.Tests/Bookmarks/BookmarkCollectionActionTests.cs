using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Bookmarks;

public sealed class BookmarkCollectionActionTests
{
  [Theory]
  [InlineData(BookmarkCollectionKind.Posts, "saved", BookmarkPredicate.Save)]
  [InlineData(BookmarkCollectionKind.RssFeedItems, "hidden", BookmarkPredicate.Hide)]
  [InlineData(BookmarkCollectionKind.Users, "following", BookmarkPredicate.Follow)]
  [InlineData(BookmarkCollectionKind.Posts, "subscribed", BookmarkPredicate.Subscribe)]
  [InlineData(BookmarkCollectionKind.Topics, "muted", BookmarkPredicate.Mute)]
  [InlineData(BookmarkCollectionKind.Hostnames, "blocked", BookmarkPredicate.Block)]
  [InlineData(BookmarkCollectionKind.Topics, "dismissed-recommendations", BookmarkPredicate.DismissRecommendation)]
  [InlineData(BookmarkCollectionKind.Communities, "proxy-following", BookmarkPredicate.ProxyFollow)]
  [InlineData(BookmarkCollectionKind.Communities, "proxy-muted", BookmarkPredicate.ProxyMute)]
  public void MutableCollectionsExposeTypedInverseActions(
      BookmarkCollectionKind kind,
      string listType,
      BookmarkPredicate predicate)
  {
    var action = Assert.IsType<BookmarkInverseAction>(BookmarkCollectionActions.For(kind, listType));
    Assert.Equal(predicate, action.Predicate);
  }

  [Theory]
  [InlineData(BookmarkCollectionKind.Topics, "viewed")]
  [InlineData(BookmarkCollectionKind.Users, "followers")]
  public void PassiveCollectionsDoNotExposeActions(BookmarkCollectionKind kind, string listType) =>
      Assert.Null(BookmarkCollectionActions.For(kind, listType));

  [Fact]
  public void DomainInverseActionsPreserveNativeIdentityAndUseTheBackendEntityType()
  {
    var row = BookmarkCollectionRowFactory.Hostname(
        new Hostname("hostname", "domain-1", "example.com"),
        BookmarkCollectionActions.For(BookmarkCollectionKind.Hostnames, "muted"),
        0);

    Assert.Equal("hostname", row.EntityType);
    Assert.Equal("/domain/example.com", row.DestinationPath);
    Assert.Equal("url_hostname", row.InverseAction?.EntityType);
  }

  [Fact]
  public void BookmarkPresentationAndAccessibilityResolveAgainAfterLocaleChanges()
  {
    var controller = new UiLocaleController(new BookmarkLanguageProvider());
    var localization = new UiLocalization(controller);
    var row = BookmarkCollectionRowFactory.Hostname(
        new Hostname("hostname", "domain-1", "example.com"),
        BookmarkCollectionActions.For(BookmarkCollectionKind.Hostnames, "muted"),
        0,
        localization);

    Assert.Equal("Domain", row.Subtitle);
    Assert.Equal("Unmute", row.ActionLabel);
    Assert.Equal("example.com. Open details", row.OpenDetailsAccessibilityLabel);

    controller.ApplySavedLocale("es");

    Assert.Equal("Dominio", row.Subtitle);
    Assert.Equal("Dejar de silenciar", row.ActionLabel);
    Assert.Equal("example.com. Abrir detalles", row.OpenDetailsAccessibilityLabel);
  }

  [Theory]
  [InlineData(NewsFeedSourceType.Article, "News source", "Source d’actualités")]
  [InlineData(NewsFeedSourceType.Podcast, "Podcast source", "Source de podcast")]
  [InlineData(NewsFeedSourceType.Video, "Video source", "Source vidéo")]
  public void RssFeedRowsUseLocalizedSourceTypeSemantics(
      NewsFeedSourceType feedType,
      string english,
      string french)
  {
    var controller = new UiLocaleController(new BookmarkLanguageProvider());
    var localization = new UiLocalization(controller);
    var feed = new RssFeedSource(
        "feed-1",
        "Feed title",
        feedType,
        null,
        null,
        null);
    var row = BookmarkCollectionRowFactory.RssFeed(feed, null, 0, localization);

    var expectedKey = feedType switch
    {
      NewsFeedSourceType.Article => UiMessageKey.NativeDotnetNewsFeedsNewsSource,
      NewsFeedSourceType.Podcast => UiMessageKey.NativeDotnetNewsFeedsPodcastSource,
      NewsFeedSourceType.Video => UiMessageKey.NativeDotnetNewsFeedsVideoSource,
      _ => throw new ArgumentOutOfRangeException(nameof(feedType), feedType, null),
    };
    Assert.Equal(expectedKey, row.SubtitleText?.Key);
    Assert.Equal(english, row.Subtitle);

    controller.ApplySavedLocale("fr");

    Assert.Equal(french, row.Subtitle);
  }

  [Fact]
  public void RssFeedRowsUseTheLocalizedGenericSourceForMissingFeedTypes()
  {
    var controller = new UiLocaleController(new BookmarkLanguageProvider());
    var localization = new UiLocalization(controller);
    var feed = new RssFeedSource(
        "feed-1",
        "Feed title",
        null,
        null,
        null,
        null);
    var row = BookmarkCollectionRowFactory.RssFeed(feed, null, 0, localization);

    Assert.Equal(UiMessageKey.NativeDotnetNewsFeedsSource, row.SubtitleText?.Key);
    Assert.Equal("Source", row.Subtitle);

    controller.ApplySavedLocale("fr");

    Assert.Equal("Source", row.Subtitle);
  }

  [Fact]
  public async Task DomainRemovalSendsTheBackendEntityType()
  {
    var bookmarkService = new DeferredBookmarkService();
    var client = new VouchaApiClient(new HttpClient(new RecordingHandler(
        ApiFixtureLoader.LoadResponse("native.bookmarks.domains.muted.default")))
    {
      BaseAddress = new Uri("https://api.test"),
    });
    var viewModel = new BookmarkCollectionViewModel(client, new SessionStore(), bookmarkService);
    viewModel.SetContext(new(
        "/my/domains/muted",
        UiText.Verbatim("Muted Domains"),
        BookmarkCollectionKind.Hostnames,
        "muted"));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var removal = viewModel.RemoveAsync(Assert.Single(viewModel.Rows), TestContext.Current.CancellationToken);

    Assert.Equal("url_hostname", Assert.Single(bookmarkService.Calls).EntityType);
    bookmarkService.Fail(new InvalidOperationException("Expected test rollback."));
    await removal;
  }

  [Theory]
  [InlineData("review", "/review/post-slug")]
  [InlineData("discussion", "/discussion/post-slug")]
  [InlineData("story", "/story/post-slug")]
  [InlineData("article", "/article/post-slug")]
  [InlineData("blog_post", "/blog-post/post-slug")]
  [InlineData("link", "/link/post-slug")]
  [InlineData("data_point", "/data-point/post-slug")]
  [InlineData("topic_recommendation", "/topic-recommendations/post-1")]
  public void PostRowsUseCanonicalSubtypeRoutes(string postType, string destination)
  {
    var post = new Post("post-1", postType, "Friendly title", null, null, Slug: "post-slug");
    Assert.Equal(destination, BookmarkCollectionRowFactory.Post(post, null, 0).DestinationPath);
  }

  [Theory]
  [InlineData("topic", "/topic/topic-slug")]
  [InlineData("rss_feed", "/source/topic-slug")]
  [InlineData("topic_recommendation", "/topic-recommendations/topic-1")]
  [InlineData("fediverse_instance", "/instance/topic-slug")]
  [InlineData("rewards_program", "/rewards-program/topic-slug")]
  public void TopicRowsUseCanonicalTypeRoutes(string topicType, string destination)
  {
    var topic = new Topic("topic-1", "Friendly topic", "topic-slug", topicType);
    Assert.Equal(destination, BookmarkCollectionRowFactory.Topic(topic, null, 0).DestinationPath);
  }

  [Theory]
  [InlineData("blog__post")]
  [InlineData("__")]
  public void UnknownRssItemMediaTypesUseExplicitProtocolPresentation(string mediaType)
  {
    var item = new RssFeedItem(
        "item-1", null, null, null, mediaType, DateTimeOffset.UnixEpoch, [], "Title", null, null, null);

    Assert.Equal(mediaType, BookmarkCollectionRowFactory.RssItem(item, null, 0).Subtitle);
  }

  [Theory]
  [InlineData("rss_feed", "source slug", "/source/source%20slug")]
  [InlineData("fediverse_instance", "instance/slug", "/instance/instance%2Fslug")]
  [InlineData("rewards_program", "reward slug", "/rewards-program/reward%20slug")]
  [InlineData("topic", null, "/topic/topic-1")]
  public void TopicRowsEscapeCanonicalIdentifiersAndFallBackToId(
      string topicType,
      string? slug,
      string destination)
  {
    var topic = new Topic("topic-1", "Friendly topic", slug!, topicType);
    Assert.Equal(destination, BookmarkCollectionRowFactory.Topic(topic, null, 0).DestinationPath);
  }

  [Theory]
  [InlineData("topic slug", "topic-id", "feed-id", "/source/topic%20slug")]
  [InlineData(null, "topic/id", "feed-id", "/source/topic%2Fid")]
  [InlineData(null, null, "feed id", "/source/feed%20id")]
  public void SourceRowsUseTopicSlugThenTopicIdThenFeedId(
      string? topicSlug,
      string? topicId,
      string feedId,
      string destination)
  {
    var topic = topicId is null ? null : new Topic(topicId, "Source topic", topicSlug!, "rss_feed");
    var feed = new RssFeedSource(
        feedId, "Source", null, null, null, null, topic, null, null, null);

    Assert.Equal(destination, BookmarkCollectionRowFactory.RssFeed(feed, null, 0).DestinationPath);
  }

  [Fact]
  public async Task RemoveIsOptimisticGuardsDuplicatesAndRollsBackAtOriginalRank()
  {
    var bookmarkService = new DeferredBookmarkService();
    var viewModel = CreateViewModel(bookmarkService, ThreePostsJson);
    viewModel.SetContext(new(
        "/my/posts/saved",
        UiText.Verbatim("Saved Posts"),
        BookmarkCollectionKind.Posts,
        "saved"));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var originalRows = viewModel.Rows.ToArray();
    var row = originalRows[1];

    var first = viewModel.RemoveAsync(row, TestContext.Current.CancellationToken);
    var duplicate = viewModel.RemoveAsync(row, TestContext.Current.CancellationToken);

    Assert.Equal(["post-1", "post-3"], viewModel.Rows.Select(candidate => candidate.Id));
    Assert.Single(bookmarkService.Calls);
    Assert.True(viewModel.IsActionPending(row));
    Assert.True(row.CanInvokeAction);
    Assert.Contains(row.Title, row.ActionAccessibilityLabel!, StringComparison.Ordinal);
    Assert.False((row with { IsActionPending = true }).CanInvokeAction);
    Assert.False(viewModel.IsActionPending(row with { EntityType = "topic" }));
    await duplicate;
    bookmarkService.Fail(new InvalidOperationException("Could not remove bookmark."));
    await first;

    Assert.Equal(originalRows.Select(candidate => candidate.Id), viewModel.Rows.Select(candidate => candidate.Id));
    Assert.Equal("Could not remove bookmark.", viewModel.MutationErrorMessage);
  }

  [Fact]
  public async Task FailedMutationDoesNotLeakIntoANewContext()
  {
    var bookmarkService = new DeferredBookmarkService();
    var viewModel = CreateViewModel(bookmarkService);
    viewModel.SetContext(new(
        "/my/posts/saved",
        UiText.Verbatim("Saved Posts"),
        BookmarkCollectionKind.Posts,
        "saved"));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var mutation = viewModel.RemoveAsync(viewModel.Rows[0], TestContext.Current.CancellationToken);

    viewModel.SetContext(new(
        "/my/users/followers",
        UiText.Verbatim("Followers"),
        BookmarkCollectionKind.Users,
        "followers"));
    bookmarkService.Fail(new InvalidOperationException("Old failure"));
    await mutation;

    Assert.Empty(viewModel.Rows);
    Assert.Null(viewModel.MutationErrorMessage);
  }

  [Fact]
  public async Task SavedCommentDestinationFetchesAndCachesItsRootPost()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(CommentPostsJson),
        new RecordedResponse(RootPostJson),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new BookmarkCollectionViewModel(client, new SessionStore());
    viewModel.SetContext(new(
        "/my/posts/saved",
        UiText.Verbatim("Saved Posts"),
        BookmarkCollectionKind.Posts,
        "saved"));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var row = Assert.Single(viewModel.Rows);
    var first = await viewModel.ResolveDestinationPathAsync(row, TestContext.Current.CancellationToken);
    var cachedRow = Assert.Single(viewModel.Rows);
    var second = await viewModel.ResolveDestinationPathAsync(cachedRow, TestContext.Current.CancellationToken);

    Assert.Equal("/discussion/root-thread/comment/comment-1", first);
    Assert.Equal(first, second);
    Assert.Equal(2, handler.Requests.Count);
    Assert.Equal("/api/v1/posts/root-1", handler.Requests[1].PathAndQuery);
  }

  [Fact]
  public async Task DestinationResolutionUsesCurrentRowState()
  {
    var viewModel = CreateViewModel(new DeferredBookmarkService());
    viewModel.SetContext(new(
        "/my/posts/saved",
        UiText.Verbatim("Saved Posts"),
        BookmarkCollectionKind.Posts,
        "saved"));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var staleRow = Assert.Single(viewModel.Rows) with { DestinationPath = "/obsolete" };

    Assert.Equal(
        "/review/saved-post",
        await viewModel.ResolveDestinationPathAsync(staleRow, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task ConcurrentSavedCommentTapsShareFetchAndOnlyFirstNavigates()
  {
    var handler = new DeferredRootHandler();
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new BookmarkCollectionViewModel(client, new SessionStore());
    viewModel.SetContext(new(
        "/my/posts/saved",
        UiText.Verbatim("Saved Posts"),
        BookmarkCollectionKind.Posts,
        "saved"));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var row = Assert.Single(viewModel.Rows);

    var first = viewModel.ResolveDestinationPathAsync(row, TestContext.Current.CancellationToken);
    Assert.Same(handler.RootStarted.Task, await Task.WhenAny(handler.RootStarted.Task, first));
    var concurrent = viewModel.ResolveDestinationPathAsync(row, TestContext.Current.CancellationToken);
    handler.ReleaseRoot.TrySetResult();

    Assert.Equal("/discussion/root-thread/comment/comment-1", await first);
    Assert.Null(await concurrent);
    Assert.Equal(
        "/discussion/root-thread/comment/comment-1",
        await viewModel.ResolveDestinationPathAsync(row, TestContext.Current.CancellationToken));
    Assert.Equal(2, handler.RequestCount);
  }

  [Fact]
  public async Task SavedCommentResolutionDoesNotCacheOrMutateReplacementContext()
  {
    var handler = new DeferredRootHandler();
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new BookmarkCollectionViewModel(client, new SessionStore());
    var savedPosts = new BookmarkCollectionRouteContext(
        "/my/posts/saved",
        UiText.Verbatim("Saved Posts"),
        BookmarkCollectionKind.Posts,
        "saved");
    viewModel.SetContext(savedPosts);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var staleRow = Assert.Single(viewModel.Rows);
    var staleResolution = viewModel.ResolveDestinationPathAsync(staleRow, TestContext.Current.CancellationToken);
    Assert.Same(handler.RootStarted.Task, await Task.WhenAny(handler.RootStarted.Task, staleResolution));
    viewModel.SetContext(new(
        "/my/users/followers",
        UiText.Verbatim("Followers"),
        BookmarkCollectionKind.Users,
        "followers"));
    handler.ReleaseRoot.TrySetResult();

    Assert.Null(await staleResolution);
    Assert.Empty(viewModel.Rows);
    Assert.Null(viewModel.NavigationErrorMessage);

    viewModel.ReportNavigationFailure();
    Assert.Null(await viewModel.ResolveDestinationPathAsync(staleRow, TestContext.Current.CancellationToken));
    Assert.Null(await viewModel.ResolveDestinationPathAsync(
        staleRow with { DestinationPath = "/obsolete" }, TestContext.Current.CancellationToken));
    Assert.Equal("We couldn't open this saved comment. Try again.", viewModel.NavigationErrorMessage);

    viewModel.SetContext(savedPosts);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var currentPath = await viewModel.ResolveDestinationPathAsync(
        Assert.Single(viewModel.Rows), TestContext.Current.CancellationToken);

    Assert.Equal("/discussion/root-thread/comment/comment-1", currentPath);
    Assert.Equal(4, handler.RequestCount);
  }

  [Fact]
  public async Task SavedCommentFetchFailureIsPresentedWithoutThrowing()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(CommentPostsJson),
        new RecordedResponse("{}", System.Net.HttpStatusCode.ServiceUnavailable),
    ]);
    var viewModel = new BookmarkCollectionViewModel(
        new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }),
        new SessionStore());
    viewModel.SetContext(new(
        "/my/posts/saved",
        UiText.Verbatim("Saved Posts"),
        BookmarkCollectionKind.Posts,
        "saved"));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var path = await viewModel.ResolveDestinationPathAsync(viewModel.Rows[0], TestContext.Current.CancellationToken);

    Assert.Null(path);
    Assert.Equal("We couldn't open this saved comment. Try again.", viewModel.NavigationErrorMessage);
  }

  private static BookmarkCollectionViewModel CreateViewModel(IBookmarkService bookmarkService, string response = PostsJson)
  {
    var client = new VouchaApiClient(new HttpClient(new RecordingHandler(response))
    {
      BaseAddress = new Uri("https://api.test"),
    });
    return new(client, new SessionStore(), bookmarkService);
  }

  private sealed class DeferredBookmarkService : IBookmarkService
  {
    private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public List<(string EntityType, string EntityId, BookmarkPredicate Predicate, bool Active)> Calls { get; } = [];

    public Task SetAsync(string entityType, string entityId, BookmarkPredicate predicate, bool active, CancellationToken cancellationToken = default)
    {
      Calls.Add((entityType, entityId, predicate, active));
      return completion.Task;
    }

    public void Fail(Exception exception) => completion.TrySetException(exception);
  }

  private sealed class SessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current { get; } = new(new User("user-1", "alice", Roles: ["member"]));
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class BookmarkLanguageProvider : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = ["en"];
  }

  private sealed class DeferredRootHandler : HttpMessageHandler
  {
    public TaskCompletionSource RootStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ReleaseRoot { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int RequestCount { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      RequestCount++;
      var body = RequestCount % 2 == 1 ? CommentPostsJson : RootPostJson;
      if (RequestCount == 2)
      {
        RootStarted.TrySetResult();
        await ReleaseRoot.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
      }
      return new(HttpStatusCode.OK) { Content = new StringContent(body), RequestMessage = request };
    }
  }

  private const string PostsJson = """
      { "results": [{ "id": "post-1", "post_type": "review", "title": "Saved post", "slug": "saved-post" }],
        "page_info": { "has_next_page": false } }
      """;

  private const string ThreePostsJson = """
      { "results": [
          { "id": "post-1", "post_type": "review", "title": "First", "slug": "first" },
          { "id": "post-2", "post_type": "review", "title": "Second", "slug": "second" },
          { "id": "post-3", "post_type": "review", "title": "Third", "slug": "third" }
        ], "page_info": { "has_next_page": false } }
      """;

  private const string CommentPostsJson = """
      { "results": [{ "id": "comment-1", "post_type": "comment", "root_post_id": "root-1" }],
        "page_info": { "has_next_page": false } }
      """;

  private const string RootPostJson = """
      { "post": { "id": "root-1", "post_type": "discussion", "slug": "root-thread" } }
      """;
}
