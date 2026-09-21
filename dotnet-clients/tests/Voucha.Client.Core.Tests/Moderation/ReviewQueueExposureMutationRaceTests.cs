using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed partial class ReviewQueueExposureViewModelTests
{
  [Fact]
  public async Task DirectExposureRefreshSupersedesPendingReveal()
  {
    var exposure = new ExposureService(
        Exposure(),
        Exposure(inCooldown: true))
    {
      HoldReveal = true,
    };
    var viewModel = Create(
        exposure,
        Post("first", true, Image("image-1")),
        Post("second", true, Image("image-2")));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var reveal = viewModel.RevealMediaAsync(
        viewModel.Items.Single(row => row.Id == "first"),
        TestContext.Current.CancellationToken);
    await exposure.RevealStarted.Task;
    await viewModel.RefreshExposureAsync(TestContext.Current.CancellationToken);

    exposure.ReleaseReveal();
    await reveal;

    Assert.False(viewModel.Items.Single(row => row.Id == "first").ShowMedia);
    var second = viewModel.Items.Single(row => row.Id == "second");
    Assert.False(second.IsExposureStale);
    Assert.True(second.IsInExposureCooldown);
    Assert.False(second.CanRevealMedia);
  }

  [Fact]
  public async Task RefreshExposureAcceptedAfterRevealStartsRemainsFresh()
  {
    var posts = new[]
    {
      Post("first", true, Image("image-1")),
      Post("second", true, Image("image-2")),
    };
    var exposure = new RacingExposureService();
    var viewModel = new ReviewQueueViewModel(
        new QueueService(posts),
        exposure,
        new AppConfig(new Uri("https://api.test")));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var reveal = viewModel.RevealMediaAsync(
        viewModel.Items.Single(row => row.Id == "first"),
        TestContext.Current.CancellationToken);
    await exposure.RevealStarted.Task;
    var refresh = viewModel.RefreshAsync(TestContext.Current.CancellationToken);
    await exposure.OlderGetStarted.Task;

    exposure.CompleteOlderGetInCooldown();
    await refresh;
    exposure.CompleteRevealInCooldown();
    await reveal;

    Assert.False(viewModel.Items.Single(row => row.Id == "first").ShowMedia);
    var second = viewModel.Items.Single(row => row.Id == "second");
    Assert.False(second.IsExposureStale);
    Assert.True(second.IsInExposureCooldown);
    Assert.False(second.CanRevealMedia);
  }

  [Fact]
  public async Task RevealSuccessAfterApprovalRemovesRowAndKeepsMediaGated()
  {
    var exposure = new ExposureService(Exposure(), Exposure()) { HoldReveal = true };
    var viewModel = Create(
        exposure,
        Post("first", true, Image("image-1")),
        Post("second", true, Image("image-2")));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var reveal = viewModel.RevealMediaAsync(
        viewModel.Items.Single(row => row.Id == "first"),
        TestContext.Current.CancellationToken);
    await exposure.RevealStarted.Task;
    await viewModel.PerformAsync(
        viewModel.Items.Single(row => row.Id == "first"),
        PostClearanceAction.Approved,
        TestContext.Current.CancellationToken);
    Assert.DoesNotContain(viewModel.Items, row => row.Id == "first");

    exposure.ReleaseReveal();
    await reveal;
    Assert.True(viewModel.Items.Single(row => row.Id == "second").IsExposureStale);

    await viewModel.RefreshAsync(TestContext.Current.CancellationToken);
    var first = viewModel.Items.Single(row => row.Id == "first");
    Assert.False(first.ShowMedia);
    Assert.True(first.CanRevealMedia);
  }
}
