using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.TopicRecommendations;
using Xunit;

namespace Voucha.Client.Core.Tests.TopicRecommendations;

public sealed class TopicRecommendationsListViewModelTests
{
  [Fact]
  public async Task LoadMoreAppendsUniquePostsAndForwardsTheCursor()
  {
    var handler = new QueueHandler(Feed(["post-1"], "cursor-1", true), Feed(["post-1", "post-2"], null, false));
    var model = new TopicRecommendationsListViewModel(new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));

    await model.LoadAsync(TestContext.Current.CancellationToken);
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["post-1", "post-2"], model.Items.Select(post => post.Id));
    Assert.False(model.HasMore);
    Assert.Equal(["/api/v1/topic-recommendations?limit=25", "/api/v1/topic-recommendations?after=cursor-1&limit=25"], handler.Paths);
  }

  [Fact]
  public async Task RequestFailureClearsLoadingAndExposesTheApiError()
  {
    var handler = new QueueHandler("failed", HttpStatusCode.BadGateway);
    var model = new TopicRecommendationsListViewModel(new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));

    await model.LoadAsync(TestContext.Current.CancellationToken);

    Assert.False(model.IsLoading);
    Assert.NotNull(model.ErrorMessage);
  }

  [Fact]
  public async Task RequestTimeoutClearsLoadingAndExposesTheTransportError()
  {
    var model = new TopicRecommendationsListViewModel(new VouchaApiClient(new HttpClient(new TimeoutHandler()) { BaseAddress = new Uri("https://api.test") }));

    await model.LoadAsync(TestContext.Current.CancellationToken);

    Assert.False(model.IsLoading);
    Assert.Equal("request timed out", model.ErrorMessage);
  }

  [Fact]
  public async Task ConcurrentLoadsAwaitTheSameActiveRequest()
  {
    var handler = new DeferredHandler(Feed(["post-1"], null, false));
    var model = new TopicRecommendationsListViewModel(new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));

    var first = model.LoadAsync(TestContext.Current.CancellationToken);
    await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    var second = model.LoadAsync(TestContext.Current.CancellationToken);

    Assert.False(second.IsCompleted);
    handler.Release.TrySetResult(true);
    await Task.WhenAll(first, second);

    Assert.Equal(["post-1"], model.Items.Select(post => post.Id));
    Assert.Single(handler.Paths);
    Assert.False(model.IsLoading);
  }

  private static string Feed(IReadOnlyList<string> ids, string? cursor, bool more)
  {
    var results = ids.Select(id => new Dictionary<string, string> { ["__entity_type"] = "post", ["id"] = id });
    var posts = ids.ToDictionary(id => id, id => new { id, post_type = "review", title = id, markdown = "body", created_by_id = "user-1" });
    return JsonSerializer.Serialize(new { results, page_info = new { has_next_page = more, end_cursor = cursor }, posts, users = new { }, communities = new { } });
  }

  private sealed class QueueHandler : HttpMessageHandler
  {
    private readonly Queue<(string Body, HttpStatusCode Status)> responses;
    public QueueHandler(params string[] responses) : this(responses.Select(body => (body, HttpStatusCode.OK)).ToArray()) { }
    public QueueHandler(string body, HttpStatusCode status) : this((body, status)) { }
    private QueueHandler(params (string Body, HttpStatusCode Status)[] responses) => this.responses = new(responses);
    public List<string?> Paths { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Paths.Add(request.RequestUri?.PathAndQuery);
      var (body, status) = responses.Dequeue();
      return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body), RequestMessage = request });
    }
  }

  private sealed class DeferredHandler(string body) : HttpMessageHandler
  {
    public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public List<string?> Paths { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Paths.Add(request.RequestUri?.PathAndQuery);
      Started.TrySetResult(true);
      await Release.Task.WaitAsync(cancellationToken);
      return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body), RequestMessage = request };
    }
  }

  private sealed class TimeoutHandler : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromException<HttpResponseMessage>(new TaskCanceledException("request timed out"));
  }
}
