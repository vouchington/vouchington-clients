using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Profiles;
using Xunit;

namespace Voucha.Client.Core.Tests.Profiles;

public sealed partial class ProfileViewModelSafetyTests
{
  [Theory]
  [InlineData(true, "Reviews (1,000+)", "All (1,005+)")]
  [InlineData(false, "Reviews (1,000)", "All (1,005)")]
  public async Task HistoryCountsShowLocalizedLowerBoundsOnlyWhenCapped(bool capped, string reviews, string all)
  {
    using var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var metrics = new UserMetrics("user_metrics", "user-2",
        new UserMetricsCount(Reviews: 1000, Discussions: 3, Comments: 2),
        CountCapped: new UserMetricsCountCapped(Reviews: capped));
    using var viewModel = NewViewModel(new User("user-2", "bob", "Hello"), metrics: metrics,
        localization: new UiLocalization(controller), localeController: controller);

    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(reviews, viewModel.HistoryTabs.Single(row => row.Tab == ProfileHistoryTab.Reviews).Label);
    Assert.Equal(all, viewModel.HistoryTabs.Single(row => row.Tab == ProfileHistoryTab.All).Label);
    Assert.Equal("Comments (2)", viewModel.HistoryTabs.Single(row => row.Tab == ProfileHistoryTab.Comments).Label);
    controller.ApplySavedLocale("es");
    Assert.Contains(capped ? "1.000+" : "1.000", viewModel.HistoryTabs.Single(row => row.Tab == ProfileHistoryTab.Reviews).Label);
  }

  [Fact]
  public async Task ExactViewerCountsDoNotInheritPublicCountCaps()
  {
    var metrics = new UserMetrics("user_metrics", "user-2",
        new UserMetricsCount(Reviews: 1000, Discussions: 3, Comments: 2),
        ViewerCount: new UserMetricsViewerCount(Reviews: 0),
        CountCapped: new UserMetricsCountCapped(Reviews: true));
    using var viewModel = NewViewModel(new User("user-2", "bob", "Hello"), metrics: metrics);

    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal("Reviews (0)", viewModel.HistoryTabs.Single(row => row.Tab == ProfileHistoryTab.Reviews).Label);
    Assert.Equal("All (5)", viewModel.HistoryTabs.Single(row => row.Tab == ProfileHistoryTab.All).Label);
  }
  [Fact]
  public async Task PrivateCountsRenderSparseOwnOverviewRowsAndRefreshLocale()
  {
    using var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var metrics = new UserMetrics("user_metrics", "user-2", new UserMetricsCount(),
        PrivateCount: new Dictionary<string, int>
        {
          ["posts_saved"] = 1000,
          ["users_muted"] = 0,
          ["topics_blocked"] = 2,
          ["unsupported"] = 99,
        },
        PrivateCountCapped: new Dictionary<string, bool> { ["posts_saved"] = true, ["users_muted"] = false });
    using var viewModel = NewViewModel(new User("user-2", "bob"), metrics: metrics,
        localization: new UiLocalization(controller), localeController: controller);

    viewModel.SetCurrentViewer("user-2", "bob");
    await viewModel.LoadOwnAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasPrivateMetrics);
    Assert.Equal(3, viewModel.PrivateMetricRows.Count);
    Assert.Contains(viewModel.PrivateMetricRows, row => row.Key == "posts_saved" && row.Label == "Saved Posts" && row.CountLabel == "1,000+");
    Assert.Contains(viewModel.PrivateMetricRows, row => row.Key == "users_muted" && row.CountLabel == "0");
    Assert.Contains(viewModel.PrivateMetricRows, row => row.Key == "topics_blocked" && row.CountLabel == "2");
    controller.ApplySavedLocale("es");
    Assert.Contains(viewModel.PrivateMetricRows, row => row.Key == "posts_saved" && row.CountLabel == "1.000+");

    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);

    Assert.False(viewModel.HasPrivateMetrics);
    Assert.Empty(viewModel.PrivateMetricRows);
  }

  [Fact]
  public async Task OwnReloadClearsPreviousPrivateRowsBeforePublishingLoadingState()
  {
    var metrics = new UserMetrics("user_metrics", "user-2", new UserMetricsCount(),
        PrivateCount: new Dictionary<string, int> { ["posts_saved"] = 1000 });
    using var viewModel = NewViewModel(new User("user-2", "bob"), metrics: metrics);
    viewModel.SetCurrentViewer("user-2", "bob");
    await viewModel.LoadOwnAsync(TestContext.Current.CancellationToken);
    Assert.Single(viewModel.PrivateMetricRows);
    var observedClearedRows = false;
    viewModel.PropertyChanged += (_, args) =>
    {
      if (args.PropertyName == nameof(ProfileViewModel.HasPrivateMetrics) && !viewModel.HasPrivateMetrics)
        observedClearedRows = true;
    };

    viewModel.SetCurrentViewer("user-2", "bob");
    await viewModel.LoadOwnAsync(TestContext.Current.CancellationToken);

    Assert.True(observedClearedRows);
    Assert.Single(viewModel.PrivateMetricRows);
  }
  [Theory]
  [InlineData(null)]
  [InlineData("user-3")]
  public async Task AuthoritativeViewerChangeClearsLoadedPrivateCounts(string? viewerId)
  {
    var metrics = new UserMetrics("user_metrics", "user-2", new UserMetricsCount(),
        PrivateCount: new Dictionary<string, int> { ["posts_saved"] = 1000 });
    using var viewModel = NewViewModel(new User("user-2", "bob"), metrics: metrics);
    viewModel.SetCurrentViewer("user-2", "bob");
    await viewModel.LoadOwnAsync(TestContext.Current.CancellationToken);
    Assert.Single(viewModel.PrivateMetricRows);

    viewModel.SetCurrentViewer(viewerId, "next");

    Assert.False(viewModel.HasPrivateMetrics);
    Assert.Empty(viewModel.PrivateMetricRows);
    viewModel.SetCurrentViewer("user-2", "bob");
    Assert.Empty(viewModel.PrivateMetricRows);
  }
}
