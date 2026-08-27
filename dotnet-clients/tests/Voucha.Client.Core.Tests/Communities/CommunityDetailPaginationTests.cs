using System.Net;
using System.Text;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Communities;

public sealed class CommunityDetailPaginationTests
{
  [Fact]
  public async Task LoadMoreModmailMessagesPrependsUniqueOlderRowsAndPreservesCurrentOverlap()
  {
    var (viewModel, handler) = CreateViewModel(
        new RecordedResponse(MessagePage(
            [("current", "current copy"), ("newer", "newer")],
            "older-cursor")),
        new RecordedResponse(MessagePage(
            [("oldest", "oldest"), ("oldest", "duplicate"), ("current", "stale copy")],
            null)));
    await viewModel.LoadModmailThreadSurfaceAsync(
        "community-1",
        "thread-1",
        TestContext.Current.CancellationToken);

    await viewModel.LoadMoreCommunityListAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["oldest", "current", "newer"], viewModel.Moderation.Select(row => row.Id));
    Assert.Equal("current copy", viewModel.Moderation.Single(row => row.Id == "current").Detail);
    Assert.Equal(
        "/api/v1/communities/community-1/modmail/thread-1/messages?after=older-cursor&limit=50",
        handler.Requests[^1].PathAndQuery);
  }

  [Fact]
  public async Task LoadMoreCommunityListPreservesRowsAndCursorForRetryAfterFailure()
  {
    var (viewModel, _) = CreateViewModel(
        new RecordedResponse(MessagePage([("current", "current")], "older-cursor")),
        new RecordedResponse("{"),
        new RecordedResponse(MessagePage([("oldest", "oldest")], null)));
    await viewModel.LoadModmailThreadSurfaceAsync(
        "community-1",
        "thread-1",
        TestContext.Current.CancellationToken);

    await viewModel.LoadMoreCommunityListAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["current"], viewModel.Moderation.Select(row => row.Id));
    Assert.True(viewModel.CanLoadMoreCommunityList);
    Assert.True(viewModel.HasCommunityListPaginationError);

    await viewModel.LoadMoreCommunityListAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["oldest", "current"], viewModel.Moderation.Select(row => row.Id));
    Assert.False(viewModel.HasCommunityListPaginationError);
  }

  [Fact]
  public async Task StaleModmailPageCannotMutateOrFinalizeAChangedSection()
  {
    var delayedPage = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
    var handler = new DelayedPaginationHandler(
        BootstrapResponses(new RecordedResponse(MessagePage([("current", "current")], "older-cursor"))),
        delayedPage.Task);
    var viewModel = CreateViewModel(handler);
    await viewModel.LoadModmailThreadSurfaceAsync(
        "community-1",
        "thread-1",
        TestContext.Current.CancellationToken);
    var older = viewModel.LoadMoreCommunityListAsync(TestContext.Current.CancellationToken);
    await handler.PaginationStarted.Task.ConfigureAwait(true);

    await viewModel.SelectSectionAsync(
        CommunityDetailSurfaceSection.Overview,
        TestContext.Current.CancellationToken);
    delayedPage.SetResult(JsonResponse(MessagePage([("stale", "stale")], null)));
    await older.ConfigureAwait(true);

    Assert.Equal(CommunityDetailSurfaceSection.Overview, viewModel.SelectedSection);
    Assert.DoesNotContain(viewModel.Moderation, row => row.Id == "stale");
    Assert.False(viewModel.CanLoadMoreCommunityList);
    Assert.False(viewModel.HasCommunityListPaginationError);
  }

  private static (CommunityDetailViewModel ViewModel, RecordingHandler Handler) CreateViewModel(
      params RecordedResponse[] paginationResponses)
  {
    var handler = new RecordingHandler(BootstrapResponses(paginationResponses));
    return (CreateViewModel(handler), handler);
  }

  private static CommunityDetailViewModel CreateViewModel(HttpMessageHandler handler)
  {
    var service = new ApiCommunitiesService(new VouchaApiClient(new HttpClient(handler)
    {
      BaseAddress = new Uri("https://api.test"),
    }));
    return new CommunityDetailViewModel(service);
  }

  private static RecordedResponse[] BootstrapResponses(params RecordedResponse[] responses) =>
  [
    new(ApiFixtureLoader.LoadResponse("web.communities.show.default")),
    new(ApiFixtureLoader.LoadResponse("web.communities.members.default")),
    new(ApiFixtureLoader.LoadResponse("web.communities.posts.default")),
    new(ApiFixtureLoader.LoadResponse("web.communities.list-items.counts.default")),
    .. responses,
  ];

  private static string MessagePage(
      IReadOnlyList<(string Id, string Body)> messages,
      string? endCursor)
      => JsonSerializer.Serialize(new
      {
        results = messages.Select(message => new
        {
          id = message.Id,
          conversation_id = "thread-1",
          body_text = message.Body,
          created_by_id = "user-1",
          sender_username = "moderator",
          created_at = "2026-07-01T00:00:00Z",
          updated_at = "2026-07-01T00:00:00Z",
          deleted_at = (string?)null,
        }),
        page_info = new
        {
          has_next_page = endCursor is not null,
          end_cursor = endCursor,
          start_cursor = (string?)null,
        },
      });

  private static HttpResponseMessage JsonResponse(string body) => new(HttpStatusCode.OK)
  {
    Content = new StringContent(body, Encoding.UTF8, "application/json"),
  };

  private sealed class DelayedPaginationHandler(
      IReadOnlyList<RecordedResponse> bootstrap,
      Task<HttpResponseMessage> paginationResponse) : HttpMessageHandler
  {
    private readonly Queue<RecordedResponse> bootstrap = new(bootstrap);
    internal TaskCompletionSource PaginationStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      if (bootstrap.TryDequeue(out var response))
      {
        return Task.FromResult(new HttpResponseMessage(response.StatusCode)
        {
          Content = new StringContent(response.Body, Encoding.UTF8, "application/json"),
          RequestMessage = request,
        });
      }

      PaginationStarted.TrySetResult();
      return paginationResponse;
    }
  }
}
