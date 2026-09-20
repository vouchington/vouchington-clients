using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed partial class ReviewQueueExposureViewModelTests
{
  [Fact]
  public async Task AppendOnlyPaginationDoesNotDiscardAnAcceptedReveal()
  {
    var exposure = new ExposureService(Exposure()) { HoldReveal = true };
    var first = Post("sensitive", true, Image("image-1"));
    var second = Post("page-two", false, Image("image-2"));
    var viewModel = new ReviewQueueViewModel(
        new PagedQueueService(first, second),
        exposure,
        new AppConfig(new Uri("https://api.test")),
        localization: null,
        localeController: null,
        utcNow: () => DateTimeOffset.UnixEpoch,
        delay: static (_, _) => Task.CompletedTask);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var reveal = viewModel.RevealMediaAsync(
        viewModel.Items.Single(row => row.Id == "sensitive"),
        TestContext.Current.CancellationToken);
    await exposure.RevealStarted.Task;

    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Contains(viewModel.Items, row => row.Id == "sensitive");
    Assert.Contains(viewModel.Items, row => row.Id == "page-two");

    exposure.ReleaseReveal();
    await reveal;

    Assert.True(viewModel.Items.Single(row => row.Id == "sensitive").ShowMedia);
    Assert.False(viewModel.Items.Single(row => row.Id == "sensitive").IsExposureStale);
  }

  [Fact]
  public async Task FailedQueueRefreshDoesNotDiscardAnAcceptedReveal()
  {
    var exposure = new ExposureService(Exposure()) { HoldReveal = true };
    var post = Post("sensitive", true, Image("image-1"));
    var viewModel = new ReviewQueueViewModel(
        new FailingRefreshQueueService(post),
        exposure,
        new AppConfig(new Uri("https://api.test")),
        localization: null,
        localeController: null,
        utcNow: () => DateTimeOffset.UnixEpoch,
        delay: static (_, _) => Task.CompletedTask);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var reveal = viewModel.RevealMediaAsync(
        Assert.Single(viewModel.Items),
        TestContext.Current.CancellationToken);
    await exposure.RevealStarted.Task;

    await viewModel.RefreshAsync(TestContext.Current.CancellationToken);
    Assert.Equal("sensitive", Assert.Single(viewModel.Items).Id);

    exposure.ReleaseReveal();
    await reveal;

    var row = Assert.Single(viewModel.Items);
    Assert.True(row.ShowMedia);
    Assert.False(row.IsExposureStale);
  }

  [Fact]
  public async Task SuccessfulQueueRefreshKeepsAcceptedExposureWhenALateRevealFinishes()
  {
    var exposure = new ExposureService(Exposure(), Exposure()) { HoldReveal = true };
    var post = Post("sensitive", true, Image("image-1"));
    var viewModel = new ReviewQueueViewModel(
        new QueueService([post]),
        exposure,
        new AppConfig(new Uri("https://api.test")),
        localization: null,
        localeController: null,
        utcNow: () => DateTimeOffset.UnixEpoch,
        delay: static (_, _) => Task.CompletedTask);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var reveal = viewModel.RevealMediaAsync(
        Assert.Single(viewModel.Items),
        TestContext.Current.CancellationToken);
    await exposure.RevealStarted.Task;

    await viewModel.RefreshAsync(TestContext.Current.CancellationToken);
    Assert.Equal("sensitive", Assert.Single(viewModel.Items).Id);
    Assert.False(Assert.Single(viewModel.Items).IsExposureStale);

    exposure.ReleaseReveal();
    await reveal;

    var row = Assert.Single(viewModel.Items);
    Assert.False(row.ShowMedia);
    Assert.False(row.IsExposureStale);
    Assert.True(row.CanRevealMedia);
  }

  private sealed class PagedQueueService(
      AdminReviewQueuePost first,
      AdminReviewQueuePost second) : ReviewQueueModerationServiceStub
  {
    public override Task<AdminReviewQueueResponse> FetchReviewQueueAsync(
        string? after = null,
        int limit = 25,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(after is null
            ? new AdminReviewQueueResponse([first], new PageInfo("next", true, null))
            : new AdminReviewQueueResponse([second], new PageInfo(null, false, null)));
  }

  private sealed class FailingRefreshQueueService(AdminReviewQueuePost post)
      : ReviewQueueModerationServiceStub
  {
    private int fetchCount;

    public override Task<AdminReviewQueueResponse> FetchReviewQueueAsync(
        string? after = null,
        int limit = 25,
        CancellationToken cancellationToken = default) =>
        Interlocked.Increment(ref fetchCount) == 1
            ? Task.FromResult(new AdminReviewQueueResponse([post], new PageInfo(null, false, null)))
            : Task.FromException<AdminReviewQueueResponse>(new HttpRequestException("offline"));
  }
}
