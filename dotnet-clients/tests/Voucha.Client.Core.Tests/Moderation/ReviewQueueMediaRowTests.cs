using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed class ReviewQueueMediaRowTests
{
  [Fact]
  public async Task MapsOrderedImageUrlsAndCaptionContentBoundary()
  {
    var post = Post(new AdminReviewQueueMediaReveal(true, [
      new("image/second", "placement/second", 2, 2, "Second caption"),
      new("image first", "placement first", 1, 1, "First caption"),
    ]));
    var viewModel = new ReviewQueueViewModel(
        new QueueService(post),
        exposureService: null,
        new AppConfig(new Uri("https://api.test"), ImageBaseUrl: new Uri("https://cdn.test/base")),
        localization: null,
        localeController: null,
        utcNow: null,
        delay: null);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var media = Assert.Single(viewModel.Items).Media;
    Assert.Equal([1, 2], media.Select(item => item.OrderIndex));
    Assert.Equal(
        "https://cdn.test/base/images/placements/placement%20first/1/image%20first?w=960",
        media[0].Source.AbsoluteUri);
    Assert.Equal("First caption", media[0].CaptionPresentation);
    Assert.Equal("First caption", media[0].CaptionText.VerbatimValue);
  }

  [Fact]
  public async Task MissingMediaProducesNoPresentationRows()
  {
    var viewModel = new ReviewQueueViewModel(new QueueService(Post(new(false, []))));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var row = Assert.Single(viewModel.Items);
    Assert.Empty(row.Media);
    Assert.False(row.HasMedia);
    Assert.False(row.ShowMedia);
  }

  private static AdminReviewQueuePost Post(AdminReviewQueueMediaReveal media) =>
      new(
          "post-1", "Title", "slug", "Preview", "discussion", "author",
          DateTimeOffset.UnixEpoch, null, null, null,
          AdminReviewQueueClearanceStatus.Rejected, null,
          new AdminModerationSummary(
              AdminModerationDisposition.Review,
              new AdminModerationEvidenceSummary(1, 2),
              ["provider_flagged"]),
          media);

  private sealed class QueueService(AdminReviewQueuePost post) : ReviewQueueModerationServiceStub
  {
    public override Task<AdminReviewQueueResponse> FetchReviewQueueAsync(
        string? after = null,
        int limit = 25,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new AdminReviewQueueResponse([post], new PageInfo(null, false, null)));
  }
}
