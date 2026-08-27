using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed class ReviewQueueViewModelPaginationStateTests
{
  [Fact]
  public async Task ApprovingOnlyActionableRowKeepsNonterminalPageLoadable()
  {
    var (viewModel, handler) = Create(
        new RecordedResponse(QueueBody(["rejected"], hasNextPage: true, endCursor: "opaque-next")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.clearance.approved")),
        new RecordedResponse(QueueBody(["in_review"])));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var row = Assert.Single(viewModel.Items);

    await viewModel.PerformAsync(row, PostClearanceAction.Approved, TestContext.Current.CancellationToken);

    Assert.Empty(viewModel.Items);
    Assert.False(viewModel.ShowEmptyState);
    Assert.True(viewModel.CanLoadMore);

    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Single(viewModel.Items);
    Assert.Equal("/api/v1/posts/review-queue?after=opaque-next&limit=25", handler.Requests[2].PathAndQuery);
  }

  [Fact]
  public async Task FilteredNonterminalPageBecomesEmptyOnlyAfterTerminalPageInfo()
  {
    var (viewModel, _) = Create(
        new RecordedResponse(QueueBody(["approved"], hasNextPage: true, endCursor: "filtered-next")),
        new RecordedResponse(QueueBody(["pending"])));
    var emptyStateNotifications = 0;
    viewModel.PropertyChanged += (_, args) =>
    {
      if (args.PropertyName == nameof(ReviewQueueViewModel.ShowEmptyState)) emptyStateNotifications++;
    };

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Empty(viewModel.Items);
    Assert.False(viewModel.ShowEmptyState);
    Assert.True(viewModel.CanLoadMore);

    emptyStateNotifications = 0;
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.False(viewModel.HasMore);
    Assert.True(viewModel.ShowEmptyState);
    Assert.True(emptyStateNotifications >= 1);
  }

  private static (ReviewQueueViewModel ViewModel, RecordingHandler Handler) Create(params RecordedResponse[] responses)
  {
    var handler = new RecordingHandler(responses);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    return (new ReviewQueueViewModel(new ApiModerationService(client)), handler);
  }

  private static string QueueBody(
      IReadOnlyList<string> statuses,
      bool hasNextPage = false,
      string? endCursor = null)
  {
    var body = JsonNode.Parse(ApiFixtureLoader.LoadResponse("native.moderation.review-queue.default"))!;
    var results = body["results"]!.AsArray();
    while (results.Count > statuses.Count) results.RemoveAt(results.Count - 1);
    for (var index = 0; index < statuses.Count; index++) results[index]!["clearance_status"] = statuses[index];
    body["page_info"]!["has_next_page"] = hasNextPage;
    body["page_info"]!["end_cursor"] = endCursor;
    return body.ToJsonString();
  }
}
