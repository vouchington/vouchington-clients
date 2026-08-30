using Voucha.Client.Core.Api;
using Voucha.Client.Core.TopicRecommendations;
using Xunit;

namespace Voucha.Client.Core.Tests.TopicRecommendations;

public sealed class TopHashtagsTests
{
  [Fact]
  public void EndpointCarriesSearchMappingAndCursor()
  {
    var request = VouchaApiEndpoints.TopHashtags("rust-lang", TopHashtagMapping.Unlinked, "next", 10);

    Assert.Equal(HttpMethod.Get, request.Method);
    Assert.Equal("/api/v1/topic-recommendations/top-hashtags", request.Path);
    Assert.Equal("rust-lang", request.Query["q"]);
    Assert.Equal("unlinked", request.Query["mapping"]);
    Assert.Equal("next", request.Query["after"]);
    Assert.Equal("10", request.Query["limit"]);
  }

  [Fact]
  public async Task LoadMoreDeduplicatesRowsAndPreservesTopics()
  {
    var service = new FakeService(
        Response([new("alias-1", "rust", 3, 3, "post-1", null)], "next", true),
        Response([new("alias-1", "rust", 3, 3, "post-1", null), new("alias-2", "dotnet", 4, 3, "post-2", "topic-2")], null, false));
    var model = new TopHashtagsViewModel(service) { Query = " rust ", Mapping = TopHashtagMapping.Unlinked };

    await model.LoadAsync(TestContext.Current.CancellationToken);
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["alias-1", "alias-2"], model.Items.Select(item => item.TopicAliasId));
    Assert.True(service.Requests[0] == ("rust", TopHashtagMapping.Unlinked, null));
    Assert.True(service.Requests[1] == ("rust", TopHashtagMapping.Unlinked, "next"));
    Assert.True(model.Topics.ContainsKey("topic-2"));
  }

  [Fact]
  public async Task ActiveSlugAliasCannotBeUnlinked()
  {
    var hashtag = new TopHashtag("alias-1", "#Swift_UI", 3, 3, "post-1", "topic-1");
    var response = new TopHashtagsResponse(
        [hashtag],
        new PageInfo(null, false, null),
        new Dictionary<string, Topic> { ["topic-1"] = new("topic-1", "Swift UI", "swift-ui", "topic") });
    var model = new TopHashtagsViewModel(new FakeService(response));

    Assert.True(model.CanUnlink(hashtag));
    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.False(model.CanUnlink(hashtag));
    Assert.True(model.CanUnlink(hashtag with { Hashtag = "#Swift_UI!" }));
  }

  [Fact]
  public async Task LoadMoreRetainsTheSubmittedQueryAndMappingAfterDraftChanges()
  {
    var service = new FakeService(
        Response([new("alias-1", "rust", 3, 3, "post-1", null)], "next", true),
        Response([new("alias-2", "dotnet", 4, 3, "post-2", "topic-2")], null, false));
    var model = new TopHashtagsViewModel(service) { Query = "rust", Mapping = TopHashtagMapping.Unlinked };

    await model.LoadAsync(TestContext.Current.CancellationToken);
    model.Query = "dotnet";
    model.Mapping = TopHashtagMapping.Linked;
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.True(service.Requests[1] == ("rust", TopHashtagMapping.Unlinked, "next"));
  }

  [Fact]
  public async Task LoadMoreDoesNotStartAnotherRequestWhileTheCurrentPageIsLoading()
  {
    var pending = new TaskCompletionSource<TopHashtagsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new FakeService(Response([new("alias-1", "rust", 3, 3, "post-1", null)], "next", true));
    var model = new TopHashtagsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    service.FetchSteps = new([_ => pending.Task]);

    var firstLoadMore = model.LoadMoreAsync(TestContext.Current.CancellationToken);
    await Task.Yield();
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Single(service.Requests.Skip(1));
    pending.SetResult(Response([], null, false));
    await firstLoadMore;
  }

  [Fact]
  public async Task NewerQueryAndMappingSupersedeAnInitialLoad()
  {
    var stale = new TaskCompletionSource<TopHashtagsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var current = new TaskCompletionSource<TopHashtagsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new FakeService() { FetchSteps = new([_ => stale.Task, _ => current.Task]) };
    var model = new TopHashtagsViewModel(service) { Query = "stale", Mapping = TopHashtagMapping.All };

    var staleLoad = model.LoadAsync(TestContext.Current.CancellationToken);
    model.Query = "current";
    model.Mapping = TopHashtagMapping.Unlinked;
    var currentLoad = model.LoadAsync(TestContext.Current.CancellationToken);
    current.SetResult(Response([new("current", "current", 3, 3, "post-current", null)], null, false));
    await currentLoad;
    stale.SetException(new HttpRequestException("stale failure"));
    await staleLoad;

    Assert.Equal(["current"], model.Items.Select(item => item.TopicAliasId));
    Assert.Null(model.ErrorMessage);
    Assert.False(model.IsLoading);
    Assert.True(service.CancellationTokens[0].IsCancellationRequested);
    Assert.True(service.Requests[1] == ("current", TopHashtagMapping.Unlinked, null));
  }

  [Fact]
  public async Task NewerQueryAndMappingSupersedeLoadMore()
  {
    var stale = new TaskCompletionSource<TopHashtagsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var current = new TaskCompletionSource<TopHashtagsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new FakeService(Response([new("initial", "initial", 3, 3, "post-initial", null)], "next", true));
    var model = new TopHashtagsViewModel(service) { Query = "initial", Mapping = TopHashtagMapping.All };
    await model.LoadAsync(TestContext.Current.CancellationToken);
    service.FetchSteps = new([_ => stale.Task, _ => current.Task]);

    var staleLoadMore = model.LoadMoreAsync(TestContext.Current.CancellationToken);
    model.Query = "current";
    model.Mapping = TopHashtagMapping.Linked;
    var currentLoad = model.LoadAsync(TestContext.Current.CancellationToken);
    current.SetResult(Response([new("current", "current", 3, 3, "post-current", "topic-current")], null, false));
    await currentLoad;
    stale.SetException(new HttpRequestException("stale failure"));
    await staleLoadMore;

    Assert.Equal(["current"], model.Items.Select(item => item.TopicAliasId));
    Assert.Null(model.ErrorMessage);
    Assert.False(model.IsLoading);
    Assert.True(service.CancellationTokens[1].IsCancellationRequested);
    Assert.True(service.Requests[2] == ("current", TopHashtagMapping.Linked, null));
  }

  [Fact]
  public async Task StaleLoadTimeoutDoesNotOverwriteTheCurrentGeneration()
  {
    var stale = new TaskCompletionSource<TopHashtagsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var current = new TaskCompletionSource<TopHashtagsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new FakeService { FetchSteps = new([_ => stale.Task, _ => current.Task]) };
    var model = new TopHashtagsViewModel(service) { Query = "stale" };

    var staleLoad = model.LoadAsync(TestContext.Current.CancellationToken);
    model.Query = "current";
    var currentLoad = model.LoadAsync(TestContext.Current.CancellationToken);
    current.SetResult(Response([new("current", "current", 3, 3, "post-current", null)], null, false));
    await currentLoad;
    stale.SetException(new TaskCanceledException("stale timed out"));
    await staleLoad;

    Assert.Equal(["current"], model.Items.Select(item => item.TopicAliasId));
    Assert.Null(model.ErrorMessage);
    Assert.False(model.IsLoading);
  }

  [Fact]
  public async Task LoadingDisablesHashtagMutationActions()
  {
    var pending = new TaskCompletionSource<TopHashtagsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new FakeService() { FetchSteps = new([_ => pending.Task]) };
    var model = new TopHashtagsViewModel(service);

    var load = model.LoadAsync(TestContext.Current.CancellationToken);
    await Task.Yield();

    Assert.True(model.IsLoading);
    Assert.False(model.CanMutate);
    pending.SetResult(Response([], null, false));
    await load;

    Assert.True(model.CanMutate);
  }

  [Fact]
  public async Task InitialLoadTimeoutRetainsItsError()
  {
    var service = new FakeService { FetchSteps = new([_ => Task.FromException<TopHashtagsResponse>(new TaskCanceledException("initial timed out"))]) };
    var model = new TopHashtagsViewModel(service);

    await model.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("initial timed out", model.ErrorMessage);
    Assert.Empty(model.Items);
    Assert.False(model.IsLoading);
  }

  [Fact]
  public async Task ContinuationLoadTimeoutRetainsItsErrorAndExistingPage()
  {
    var service = new FakeService(
        Response([new("alias-1", "rust", 3, 3, "post-1", null)], "next", true))
    {
      FetchSteps = new([_ => Task.FromResult(Response([new("alias-1", "rust", 3, 3, "post-1", null)], "next", true)), _ => Task.FromException<TopHashtagsResponse>(new TaskCanceledException("more timed out"))]),
    };
    var model = new TopHashtagsViewModel(service);

    await model.LoadAsync(TestContext.Current.CancellationToken);
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal("more timed out", model.ErrorMessage);
    Assert.Equal(["alias-1"], model.Items.Select(item => item.TopicAliasId));
    Assert.True(model.HasMore);
    Assert.False(model.IsLoading);
  }

  [Fact]
  public async Task CallerCanceledLoadDoesNotSurfaceAnError()
  {
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    var service = new FakeService
    {
      FetchSteps = new([_ => Task.FromException<TopHashtagsResponse>(new OperationCanceledException(cancellation.Token))]),
    };
    var model = new TopHashtagsViewModel(service);

    await model.LoadAsync(cancellation.Token);

    Assert.Null(model.ErrorMessage);
    Assert.Empty(model.Items);
    Assert.False(model.IsLoading);
  }

  [Fact]
  public async Task CreateTopicUsesTheSelectedAliasAsItsSource()
  {
    var service = new FakeService(Response([], null, false));
    var model = new TopHashtagsViewModel(service);

    await model.CreateTopicAsync(new("alias-1", "Rust_lang.v2", 3, 3, "post-1", null), "Rust", TestContext.Current.CancellationToken);

    Assert.Equal("alias-1", service.CreatedRequest?.SourceTopicAliasId);
    Assert.Equal("rust-lang-v2", service.CreatedRequest?.Slug);
  }

  [Fact]
  public async Task MutationFailureRetainsItsErrorWithoutReloading()
  {
    var service = new FakeService(Response([], null, false)) { LinkException = new HttpRequestException("link failed") };
    var model = new TopHashtagsViewModel(service);

    await model.LinkAsync(new("alias-1", "rust", 3, 3, "post-1", null), "topic-1", TestContext.Current.CancellationToken);

    Assert.Equal("link failed", model.ErrorMessage);
    Assert.Empty(service.Requests);
  }

  [Theory]
  [InlineData("unlink")]
  [InlineData("create")]
  public async Task EveryMutationFailureRetainsItsErrorWithoutReloading(string mutation)
  {
    var service = new FakeService(Response([], null, false)) { MutationException = new HttpRequestException($"{mutation} failed") };
    var model = new TopHashtagsViewModel(service);
    var hashtag = new TopHashtag("alias-1", "rust", 3, 3, "post-1", "topic-1");

    if (mutation == "unlink") await model.UnlinkAsync(hashtag, TestContext.Current.CancellationToken);
    else await model.CreateTopicAsync(hashtag, "Rust", TestContext.Current.CancellationToken);

    Assert.Equal($"{mutation} failed", model.ErrorMessage);
    Assert.Empty(service.Requests);
  }

  [Theory]
  [InlineData("link")]
  [InlineData("unlink")]
  [InlineData("create")]
  public async Task MutationTimeoutRetainsItsErrorWithoutReloading(string mutation)
  {
    var timeout = new TaskCanceledException($"{mutation} timed out");
    var service = new FakeService(Response([], null, false))
    {
      LinkException = timeout,
      MutationException = timeout,
    };
    var model = new TopHashtagsViewModel(service);
    var hashtag = new TopHashtag("alias-1", "rust", 3, 3, "post-1", "topic-1");

    if (mutation == "link") await model.LinkAsync(hashtag, "topic-1", TestContext.Current.CancellationToken);
    else if (mutation == "unlink") await model.UnlinkAsync(hashtag, TestContext.Current.CancellationToken);
    else await model.CreateTopicAsync(hashtag, "Rust", TestContext.Current.CancellationToken);

    Assert.Equal($"{mutation} timed out", model.ErrorMessage);
    Assert.Empty(service.Requests);
  }

  [Fact]
  public async Task CallerCanceledMutationDoesNotSurfaceAnError()
  {
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    var service = new FakeService(Response([], null, false))
    {
      LinkException = new OperationCanceledException(cancellation.Token),
    };
    var model = new TopHashtagsViewModel(service);

    await model.LinkAsync(new("alias-1", "rust", 3, 3, "post-1", null), "topic-1", cancellation.Token);

    Assert.Null(model.ErrorMessage);
    Assert.Empty(service.Requests);
  }

  private static TopHashtagsResponse Response(IReadOnlyList<TopHashtag> rows, string? cursor, bool hasMore) =>
      new(rows, new PageInfo(cursor, hasMore, null), new Dictionary<string, Topic>(
          rows.Where(row => row.TopicId is not null).ToDictionary(
              row => row.TopicId!,
              row => new Topic(row.TopicId!, row.Hashtag, row.Hashtag, "topic"))));

  private sealed class FakeService(params TopHashtagsResponse[] responses) : ITopHashtagsService
  {
    private int index;
    public List<(string? Query, TopHashtagMapping Mapping, string? After)> Requests { get; } = [];
    public List<CancellationToken> CancellationTokens { get; } = [];
    public Queue<Func<CancellationToken, Task<TopHashtagsResponse>>>? FetchSteps { get; set; }
    public CreateTopicRequest? CreatedRequest { get; private set; }
    public Exception? LinkException { get; init; }
    public Exception? MutationException { get; init; }

    public Task<TopHashtagsResponse> FetchAsync(string? query = null, TopHashtagMapping mapping = TopHashtagMapping.All, string? after = null, int limit = 25, CancellationToken cancellationToken = default)
    {
      Requests.Add((query, mapping, after));
      CancellationTokens.Add(cancellationToken);
      return FetchSteps is { Count: > 0 } ? FetchSteps.Dequeue()(cancellationToken) : Task.FromResult(responses[index++]);
    }

    public Task LinkAsync(string topicId, string aliasId, CancellationToken cancellationToken = default) =>
        LinkException is { } exception ? Task.FromException(exception) : Task.CompletedTask;
    public Task UnlinkAsync(string topicId, string aliasId, CancellationToken cancellationToken = default) =>
        MutationException is { } exception ? Task.FromException(exception) : Task.CompletedTask;
    public Task<TopicMutationResponse> CreateTopicAsync(CreateTopicRequest request, CancellationToken cancellationToken = default)
    {
      if (MutationException is { } exception) return Task.FromException<TopicMutationResponse>(exception);
      CreatedRequest = request;
      return Task.FromResult(new TopicMutationResponse(new Topic("topic-1", request.Name, request.Slug, request.TopicType)));
    }
  }
}
