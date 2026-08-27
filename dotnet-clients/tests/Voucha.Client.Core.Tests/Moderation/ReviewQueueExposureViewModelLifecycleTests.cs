using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed partial class ReviewQueueExposureViewModelTests
{
  [Fact]
  public async Task RapidReappearanceQueuesExposureRefreshUntilCanceledRequestSettles()
  {
    var exposure = new PageLifecycleExposureService();
    var viewModel = new ReviewQueueViewModel(
        new QueueService([Post("sensitive", true, Image("image-1"))]),
        exposure,
        new AppConfig(new Uri("https://api.test")));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    using var oldPage = new CancellationTokenSource();

    var staleRefresh = viewModel.RefreshExposureAsync(oldPage.Token);
    await exposure.StaleGetStarted.Task;
    viewModel.CancelExposureOperations();
    oldPage.Cancel();
    var resumedRefresh = viewModel.ResumeAsync(TestContext.Current.CancellationToken);

    Assert.False(resumedRefresh.IsCompleted);
    Assert.Equal(2, exposure.FetchCount);
    exposure.CompleteStaleGet();
    await Task.WhenAll(staleRefresh, resumedRefresh);

    var resumed = Assert.Single(viewModel.Items);
    Assert.Equal(3, exposure.FetchCount);
    Assert.False(resumed.IsExposureStale);
    Assert.True(resumed.IsInExposureCooldown);
    Assert.False(resumed.CanRevealMedia);
  }

  [Fact]
  public async Task SameLifecycleWaiterRetriesCanceledRefreshOnlyWhileStateIsStale()
  {
    var exposure = new PageLifecycleExposureService();
    var viewModel = new ReviewQueueViewModel(
        new QueueService([Post("sensitive", true, Image("image-1"))]),
        exposure,
        new AppConfig(new Uri("https://api.test")));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    viewModel.CancelExposureOperations();
    using var canceledCaller = new CancellationTokenSource();

    var canceledRefresh = viewModel.RefreshExposureAsync(canceledCaller.Token);
    await exposure.StaleGetStarted.Task;
    canceledCaller.Cancel();
    var validRefresh = viewModel.RefreshExposureAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, exposure.FetchCount);
    exposure.CompleteStaleGet();
    await Task.WhenAll(canceledRefresh, validRefresh);

    Assert.Equal(3, exposure.FetchCount);
    Assert.False(Assert.Single(viewModel.Items).IsExposureStale);
  }

  [Fact]
  public async Task SameLifecycleWaiterSkipsDuplicateAfterSuccessfulRefresh()
  {
    var exposure = new PageLifecycleExposureService();
    var viewModel = new ReviewQueueViewModel(
        new QueueService([Post("sensitive", true, Image("image-1"))]),
        exposure,
        new AppConfig(new Uri("https://api.test")));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var first = viewModel.RefreshExposureAsync(TestContext.Current.CancellationToken);
    await exposure.StaleGetStarted.Task;
    var waiter = viewModel.RefreshExposureAsync(TestContext.Current.CancellationToken);
    exposure.CompleteStaleGet();
    await Task.WhenAll(first, waiter);

    Assert.Equal(2, exposure.FetchCount);
  }
}
