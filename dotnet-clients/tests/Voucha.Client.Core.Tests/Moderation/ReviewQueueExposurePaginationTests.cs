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
}
