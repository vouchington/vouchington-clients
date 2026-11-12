using Voucha.Client.Core.Api;
using Voucha.Client.Core.ReferralLinks;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.ReferralLinks;

public sealed partial class ReferralLinksViewModelTests
{
  [Theory]
  [InlineData(false, "follow_users")]
  [InlineData(true, "mutual_follows")]
  public async Task FeedPagingFollowsOpaqueCursorsAcrossShortAndEmptyPages(bool mutual, string feedType)
  {
    var requests = new List<FetchReferralLinksFeedRequest>();
    var pages = new Queue<ReferralLinkFeedResponse>([
      FeedPage(["first"], "opaque/+one", true),
      FeedPage([], "opaque/+two", true),
      FeedPage(["first", "last"], null, false),
    ]);
    var service = new RecordingReferralLinksService
    {
      FetchFeed = request =>
      {
        requests.Add(request);
        return Task.FromResult(pages.Dequeue());
      },
    };
    using var viewModel = new ReferralLinksViewModel(service);

    await viewModel.LoadFeedAsync(mutual, TestContext.Current.CancellationToken);
    Assert.True(viewModel.HasMoreFeed);
    await viewModel.LoadMoreFeedAsync(TestContext.Current.CancellationToken);
    Assert.False(viewModel.HasMoreFeed);
    await viewModel.LoadMoreFeedAsync(TestContext.Current.CancellationToken);

    Assert.Equal([null, "opaque/+one", "opaque/+two"], requests.Select(request => request.After));
    Assert.All(requests, request => Assert.Equal(feedType, request.FeedType));
    Assert.Equal(["first", "last"], viewModel.Items.Select(item => item.Id));
    Assert.False(viewModel.HasMoreFeed);
  }

  [Fact]
  public async Task InitialEmptyFeedPagesAdvanceUntilRowsAreVisible()
  {
    var requests = new List<FetchReferralLinksFeedRequest>();
    var pages = new Queue<ReferralLinkFeedResponse>([
      FeedPage([], "first-empty", true),
      FeedPage([], "second-empty", true),
      FeedPage(["visible"], "next", true),
    ]);
    var service = new RecordingReferralLinksService
    {
      FetchFeed = request =>
      {
        requests.Add(request);
        return Task.FromResult(pages.Dequeue());
      },
    };
    using var viewModel = new ReferralLinksViewModel(service);
    await viewModel.LoadFeedAsync(cancellationToken: TestContext.Current.CancellationToken);
    Assert.Equal([null, "first-empty", "second-empty"], requests.Select(request => request.After));
    Assert.Equal(["visible"], viewModel.Items.Select(item => item.Id));
    Assert.True(viewModel.HasMoreFeed);
  }

  [Fact]
  public async Task FeedAppendFailurePreservesRowsAndRetriesTheSameCursor()
  {
    var service = new RecordingReferralLinksService { FeedResponse = FeedPage(["first"], "next", true) };
    using var viewModel = new ReferralLinksViewModel(service);
    await viewModel.LoadFeedAsync(cancellationToken: TestContext.Current.CancellationToken);
    service.FetchFeed = request => request.After == "next"
        ? Task.FromResult(FeedPage([], "failed-page", true))
        : Task.FromException<ReferralLinkFeedResponse>(new HttpRequestException("offline"));
    await viewModel.LoadMoreFeedAsync(TestContext.Current.CancellationToken);
    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.Equal(["first"], viewModel.Items.Select(item => item.Id));
    Assert.True(viewModel.HasMoreFeed);

    service.FetchFeed = _ => Task.FromResult(FeedPage(["last"], null, false));
    await viewModel.LoadMoreFeedAsync(TestContext.Current.CancellationToken);
    Assert.Equal("failed-page", service.LastFeedRequest?.After);
    Assert.Equal(["first", "last"], viewModel.Items.Select(item => item.Id));
    Assert.False(viewModel.HasError);
  }

  [Fact]
  public async Task SwitchingFeedIgnoresPendingAppendAndResetsTheCursor()
  {
    var pending = new TaskCompletionSource<ReferralLinkFeedResponse>();
    var service = new RecordingReferralLinksService { FeedResponse = FeedPage(["following"], "next", true) };
    using var viewModel = new ReferralLinksViewModel(service);
    await viewModel.LoadFeedAsync(cancellationToken: TestContext.Current.CancellationToken);
    var appendRequests = 0;
    service.FetchFeed = _ =>
    {
      appendRequests++;
      return pending.Task;
    };
    var append = viewModel.LoadMoreFeedAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMoreFeedAsync(TestContext.Current.CancellationToken);
    Assert.Equal(1, appendRequests);
    service.FetchFeed = _ => Task.FromResult(FeedPage(["mutual"], null, false));
    await viewModel.LoadFeedAsync(mutual: true, TestContext.Current.CancellationToken);
    Assert.Null(service.LastFeedRequest?.After);
    pending.SetResult(FeedPage(["stale"], "stale-next", true));
    await append;
    Assert.Equal(["mutual"], viewModel.Items.Select(item => item.Id));
    Assert.False(viewModel.HasMoreFeed);
  }

  [Fact]
  public async Task CanceledAppendCanRetryAndSwitchingToMineDisablesFeedPaging()
  {
    var service = new RecordingReferralLinksService
    {
      FeedResponse = FeedPage(["first"], "next", true),
      MineResponse = MineResponse("mine", "https://example.com/mine", new PageInfo(null, false, null)),
    };
    using var viewModel = new ReferralLinksViewModel(service);
    await viewModel.LoadFeedAsync(cancellationToken: TestContext.Current.CancellationToken);
    service.FetchFeed = _ => Task.FromCanceled<ReferralLinkFeedResponse>(new CancellationToken(true));
    await viewModel.LoadMoreFeedAsync(TestContext.Current.CancellationToken);
    Assert.Equal(LoadState.Idle, viewModel.State);
    Assert.True(viewModel.HasMoreFeed);
    Assert.Equal(["first"], viewModel.Items.Select(item => item.Id));
    service.FetchFeed = _ => Task.FromResult(FeedPage(["second"], "later", true));
    await viewModel.LoadMoreFeedAsync(TestContext.Current.CancellationToken);
    Assert.Equal("next", service.LastFeedRequest?.After);
    Assert.Equal(["first", "second"], viewModel.Items.Select(item => item.Id));

    await viewModel.LoadMineAsync(TestContext.Current.CancellationToken);
    service.FetchFeed = _ => throw new InvalidOperationException("Inactive feed requested");
    await viewModel.LoadMoreFeedAsync(TestContext.Current.CancellationToken);
    Assert.False(viewModel.HasMoreFeed);
    Assert.Equal(["mine"], viewModel.Items.Select(item => item.Id));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task EmptyTerminalOrRepeatedCursorStopsPaging(bool repeatedCursor)
  {
    var service = new RecordingReferralLinksService { FeedResponse = FeedPage(["first"], "next", true) };
    using var model = new ReferralLinksViewModel(service);
    await model.LoadFeedAsync(cancellationToken: TestContext.Current.CancellationToken);
    service.FetchFeed = _ => Task.FromResult(FeedPage([], repeatedCursor ? "next" : null, repeatedCursor));
    await model.LoadMoreFeedAsync(TestContext.Current.CancellationToken);
    Assert.False(model.HasMoreFeed);
    Assert.Equal(["first"], model.Items.Select(row => row.Id));
    Assert.Equal(LoadState.Loaded, model.State);
  }

  [Fact]
  public async Task ErrorAfterInitialEmptyPageRetriesTheFailedCursor()
  {
    var service = new RecordingReferralLinksService
    {
      FetchFeed = request => request.After is null
          ? Task.FromResult(FeedPage([], "failed", true))
          : Task.FromException<ReferralLinkFeedResponse>(new HttpRequestException("offline")),
    };
    using var model = new ReferralLinksViewModel(service);
    await model.LoadFeedAsync(cancellationToken: TestContext.Current.CancellationToken);
    Assert.True(model.HasError);
    Assert.True(model.HasMoreFeed);
    Assert.Empty(model.Items);
    service.FetchFeed = _ => Task.FromResult(FeedPage(["recovered"], null, false));
    await model.LoadMoreFeedAsync(TestContext.Current.CancellationToken);
    Assert.Equal("failed", service.LastFeedRequest?.After);
    Assert.Equal(["recovered"], model.Items.Select(row => row.Id));
  }

  private static ReferralLinkFeedResponse FeedPage(string[] ids, string? cursor, bool hasMore) =>
      new(ids.Select(id => new ReferralLinkFeedItem(
          id, "user", "program", "Program", "program", "https://example.com/ref", id)).ToArray(),
          new Dictionary<string, ReferralLinkFeedUser>(StringComparer.Ordinal),
          new PageInfo(cursor, hasMore, null));
}
