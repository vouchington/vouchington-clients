using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed partial class ReviewQueueExposureViewModelTests
{
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
