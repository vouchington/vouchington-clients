using System.Net;
using System.Text;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Bookmarks;

public sealed class BookmarkCollectionViewModelTests
{
  [Fact]
  public async Task LoadAsyncMapsEveryCollectionKindThroughTheClient()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(PostsJson()),
        new RecordedResponse(TopicsJson()),
        new RecordedResponse(UsersJson()),
        new RecordedResponse(RssFeedItemsJson()),
        new RecordedResponse(RssFeedsJson()),
        new RecordedResponse(UrlsJson()),
        new RecordedResponse(HostnamesJson()),
        new RecordedResponse(CommunitiesJson()),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new BookmarkCollectionViewModel(client, new TestSessionStore(new User(
        "user-1",
        "alice",
        Roles: ["member"],
        EmailAddress: "alice@example.com",
        MembershipPlan: "membership")));

    var cases = new[]
    {
      new
      {
        Context = new BookmarkCollectionRouteContext("/my/posts/saved", UiText.Verbatim("Saved Posts"), BookmarkCollectionKind.Posts, "saved"),
        Title = "Saved Posts",
        Path = "/api/v1/users/user-1/posts/saved?limit=25",
        Row = ExpectedRow("post-1", "post", "Saved post post-1", "Discussion", null),
        Destination = FirstPostDestination(),
        Predicate = BookmarkPredicate.Save,
        ActionEntityType = "post",
      },
      new
      {
        Context = new BookmarkCollectionRouteContext("/my/topics/muted", UiText.Verbatim("Muted Topics"), BookmarkCollectionKind.Topics, "muted"),
        Title = "Muted Topics",
        Path = "/api/v1/users/user-1/topics/muted?limit=25",
        Row = ExpectedRow("topic-1", "topic", "Test Topic", "Topic", null),
        Destination = "/topic/topic-1",
        Predicate = BookmarkPredicate.Mute,
        ActionEntityType = "topic",
      },
      new
      {
        Context = new BookmarkCollectionRouteContext("/my/friend-recommendations/dismissed", UiText.Verbatim("Dismissed Recommendations"), BookmarkCollectionKind.Users, "dismissed-recommendations"),
        Title = "Dismissed Recommendations",
        Path = "/api/v1/users/user-1/users/dismissed-recommendations?limit=25",
        Row = ExpectedRow("user-2", "user", "User", null, null),
        Destination = "/user/user-2",
        Predicate = BookmarkPredicate.DismissRecommendation,
        ActionEntityType = "user",
      },
      new
      {
        Context = new BookmarkCollectionRouteContext("/my/news-items/saved", UiText.Verbatim("Saved News"), BookmarkCollectionKind.RssFeedItems, "saved", MediaType: "article"),
        Title = "Saved News",
        Path = "/api/v1/users/user-1/rss-feed-items/saved?limit=25&media_type=article",
        Row = ExpectedRow("item-1", "rss_feed_item", "Test Article", "Article", "Example Feed"),
        Destination = "/news?rss_item=item-1",
        Predicate = BookmarkPredicate.Save,
        ActionEntityType = "rss_feed_item",
      },
      new
      {
        Context = new BookmarkCollectionRouteContext("/my/news-sources/muted", UiText.Verbatim("Muted News Sources"), BookmarkCollectionKind.RssFeeds, "muted", FeedType: "article"),
        Title = "Muted News Sources",
        Path = "/api/v1/users/user-1/rss-feeds/muted?feed_type=article&limit=25",
        Row = ExpectedRow("feed-1", "rss_feed", "Example Feed", "Article", "Test Topic"),
        Destination = "/source/test-topic",
        Predicate = BookmarkPredicate.Mute,
        ActionEntityType = "rss_feed",
      },
      new
      {
        Context = new BookmarkCollectionRouteContext("/my/urls/saved", UiText.Verbatim("Saved Links"), BookmarkCollectionKind.Urls, "saved"),
        Title = "Saved Links",
        Path = "/api/v1/users/user-1/urls/saved?limit=25",
        Row = ExpectedRow(
            "url-1",
            "url",
            "https://example.com/guides/native-clients",
            "example.com",
            "/guides/native-clients"),
        Destination = "/url/url-1",
        Predicate = BookmarkPredicate.Save,
        ActionEntityType = "url",
      },
      new
      {
        Context = new BookmarkCollectionRouteContext("/my/domains/muted", UiText.Verbatim("Muted Domains"), BookmarkCollectionKind.Hostnames, "muted"),
        Title = "Muted Domains",
        Path = "/api/v1/users/user-1/domains/muted?limit=25",
        Row = ExpectedRow("domain-2", "hostname", "domain-2.example.com", "Domain", null),
        Destination = "/domain/domain-2.example.com",
        Predicate = BookmarkPredicate.Mute,
        ActionEntityType = "url_hostname",
      },
      new
      {
        Context = new BookmarkCollectionRouteContext("/my/communities/proxy-following", UiText.Verbatim("Proxy Following"), BookmarkCollectionKind.Communities, "proxy-following"),
        Title = "Proxy Following",
        Path = "/api/v1/users/user-1/communities/proxy-following?limit=25",
        Row = ExpectedRow("community-1", "community", "Test Community", "Public", null),
        Destination = "/communities/test-community",
        Predicate = BookmarkPredicate.ProxyFollow,
        ActionEntityType = "community",
      },
    };

    foreach (var testCase in cases)
    {
      viewModel.SetContext(testCase.Context);
      await viewModel.LoadAsync(TestContext.Current.CancellationToken);

      Assert.True(viewModel.HasContext);
      Assert.Equal(testCase.Title, viewModel.Title);
      Assert.Null(viewModel.ErrorMessage);
      Assert.NotEmpty(viewModel.Rows);
      Assert.Equal(testCase.Row.Id, viewModel.Rows[0].Id);
      Assert.Equal(testCase.Row.Title, viewModel.Rows[0].Title);
      Assert.NotEqual(viewModel.Rows[0].Id, viewModel.Rows[0].Title);
      Assert.All(
          new[] { viewModel.Rows[0].Title, viewModel.Rows[0].Subtitle, viewModel.Rows[0].Detail }
              .Where(value => value is not null),
          value => Assert.NotEqual(viewModel.Rows[0].Id, value));
      Assert.Equal(testCase.Row.EntityType, viewModel.Rows[0].EntityType);
      Assert.Equal(testCase.Destination, viewModel.Rows[0].DestinationPath);
      Assert.Equal(testCase.Predicate, viewModel.Rows[0].InverseAction?.Predicate);
      Assert.Equal(testCase.ActionEntityType, viewModel.Rows[0].InverseAction?.EntityType);
      Assert.Equal(0, viewModel.Rows[0].Rank);
    }

    Assert.Equal(cases.Select(testCase => testCase.Path), handler.Requests.Select(request => request.PathAndQuery));
  }

  [Fact]
  public async Task LoadAsyncWithoutContextOrIdentityLeavesTheViewModelEmpty()
  {
    var handler = new RecordingHandler("{}");
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new BookmarkCollectionViewModel(client, new TestSessionStore(null));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.False(viewModel.HasContext);
    Assert.Empty(viewModel.Rows);
    Assert.Null(viewModel.ErrorMessage);
    Assert.False(viewModel.IsLoading);
    Assert.Empty(handler.Requests);

    viewModel.SetContext(new BookmarkCollectionRouteContext("/my/posts/saved", UiText.Verbatim("Saved Posts"), BookmarkCollectionKind.Posts, "saved"));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Sign in to view bookmarks.", viewModel.ErrorMessage);
    Assert.Empty(viewModel.Rows);
    Assert.False(viewModel.IsLoading);
    Assert.Empty(handler.Requests);
  }

  [Fact]
  public async Task LoadAsyncCapturesClientFailures()
  {
    var handler = new RecordingHandler([new RecordedResponse("{", HttpStatusCode.OK)]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new BookmarkCollectionViewModel(client, new TestSessionStore(new User(
        "user-1",
        "alice",
        Roles: ["member"],
        EmailAddress: "alice@example.com",
        MembershipPlan: "membership")));

    viewModel.SetContext(new BookmarkCollectionRouteContext("/my/posts/saved", UiText.Verbatim("Saved Posts"), BookmarkCollectionKind.Posts, "saved"));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasError);
    Assert.Empty(viewModel.Rows);
    Assert.False(viewModel.IsLoading);
    Assert.NotNull(viewModel.ErrorMessage);
  }

  [Fact]
  public async Task LoadAsyncIgnoresConcurrentRequests()
  {
    var handler = new DeferredQueueHandler([PostsJson()]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new BookmarkCollectionViewModel(client, new TestSessionStore(new User(
        "user-1",
        "alice",
        Roles: ["member"],
        EmailAddress: "alice@example.com",
        MembershipPlan: "membership")));

    viewModel.SetContext(new BookmarkCollectionRouteContext("/my/posts/saved", UiText.Verbatim("Saved Posts"), BookmarkCollectionKind.Posts, "saved"));

    var first = viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await handler.Started.Task;

    var second = viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await second;

    Assert.Single(handler.Requests);
    handler.Release.TrySetResult();

    await first;

    Assert.False(viewModel.IsLoading);
    Assert.Equal(2, viewModel.Rows.Count);
  }

  [Fact]
  public async Task LoadAsyncPublishesOnlyTheCurrentContextDuringAContextSwitch()
  {
    var handler = new ContextSwitchHandler();
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new BookmarkCollectionViewModel(client, new TestSessionStore(new User(
        "user-1",
        "alice",
        Roles: ["member"],
        EmailAddress: "alice@example.com",
        MembershipPlan: "membership")));

    viewModel.SetContext(new BookmarkCollectionRouteContext(
        "/my/posts/saved",
        UiText.Verbatim("Saved Posts"),
        BookmarkCollectionKind.Posts,
        "saved"));
    var staleLoad = viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await handler.PostsStarted.Task;

    viewModel.SetContext(new BookmarkCollectionRouteContext(
        "/my/topics/muted",
        UiText.Verbatim("Muted Topics"),
        BookmarkCollectionKind.Topics,
        "muted"));
    var currentLoad = viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await handler.TopicsStarted.Task;

    handler.ReleasePosts.TrySetResult();
    await staleLoad;

    Assert.True(viewModel.IsLoading);
    Assert.Equal("Muted Topics", viewModel.Title);
    Assert.Empty(viewModel.Rows);
    Assert.Null(viewModel.ErrorMessage);

    handler.ReleaseTopics.TrySetResult();
    await currentLoad;

    Assert.False(viewModel.IsLoading);
    var row = Assert.Single(viewModel.Rows);
    Assert.Equal("topic-1", row.Id);
    Assert.Null(viewModel.ErrorMessage);
  }

  private sealed class TestSessionStore : ISessionStore
  {
    public TestSessionStore(User? identity) => Current = new SessionSnapshot(identity);

    public event EventHandler<SessionChangedEventArgs>? SessionChanged
    {
      add { }
      remove { }
    }

    public SessionSnapshot Current { get; }

    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private static BookmarkCollectionRow ExpectedRow(
      string id,
      string entityType,
      string title,
      string? subtitle,
      string? detail) =>
      new(
          id,
          entityType,
          UiText.Verbatim(title),
          UiLocalization.English,
          subtitle is null ? null : UiText.Verbatim(subtitle),
          detail is null ? null : UiText.Verbatim(detail));

  private static string PostsJson() => ApiFixtureLoader.LoadResponse("native.bookmarks.posts.saved.default");

  private static string FirstPostDestination()
  {
    using var document = JsonDocument.Parse(PostsJson());
    var firstPost = document.RootElement.GetProperty("results")[0];
    var slug = firstPost.GetProperty("slug").GetString()
        ?? throw new InvalidOperationException("The canonical saved-post fixture must provide a slug.");
    return $"/discussion/{slug}";
  }

  private static string TopicsJson() => ApiFixtureLoader.LoadResponse("native.bookmarks.topics.muted.default");

  private static string UsersJson() => ApiFixtureLoader.LoadResponse("native.bookmarks.users.dismissed-recommendations.default");

  private static string RssFeedItemsJson() => ApiFixtureLoader.LoadResponse("native.bookmarks.rss-feed-items.saved.default");

  private static string RssFeedsJson() => ApiFixtureLoader.LoadResponse("native.bookmarks.rss-feeds.muted.default");

  private static string UrlsJson() => ApiFixtureLoader.LoadResponse("native.bookmarks.urls.saved.default");

  private static string HostnamesJson() => ApiFixtureLoader.LoadResponse("native.bookmarks.domains.muted.default");

  private static string CommunitiesJson() => ApiFixtureLoader.LoadResponse("native.bookmarks.communities.proxy-following.default");

  private sealed class DeferredQueueHandler : HttpMessageHandler
  {
    private readonly Queue<string> responses;

    public DeferredQueueHandler(IEnumerable<string> responses)
    {
      this.responses = new Queue<string>(responses);
    }

    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public List<RecordedRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
      Requests.Add(new RecordedRequest(request.Method, request.RequestUri?.PathAndQuery, body));
      Started.TrySetResult();
      await Release.Task.ConfigureAwait(false);

      return new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(
            responses.Count > 0 ? responses.Dequeue() : "{}",
            Encoding.UTF8,
            "application/json"),
        RequestMessage = request,
      };
    }
  }

  private sealed class ContextSwitchHandler : HttpMessageHandler
  {
    public TaskCompletionSource PostsStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource TopicsStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource ReleasePosts { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource ReleaseTopics { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      var isPostsRequest = request.RequestUri?.AbsolutePath.Contains("/posts/", StringComparison.Ordinal) == true;
      var started = isPostsRequest ? PostsStarted : TopicsStarted;
      var release = isPostsRequest ? ReleasePosts : ReleaseTopics;
      started.TrySetResult();
      await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

      return new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(isPostsRequest ? "{" : TopicsJson(), Encoding.UTF8, "application/json"),
        RequestMessage = request,
      };
    }
  }
}
