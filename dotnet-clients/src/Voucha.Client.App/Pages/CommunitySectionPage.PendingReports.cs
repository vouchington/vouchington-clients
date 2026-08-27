using Voucha.Client.App.Controls;
using Voucha.Client.Core.Communities;

namespace Voucha.Client.App.Pages;

public abstract partial class CommunitySectionPage
{
  private readonly CollectionView pendingReportsView = new()
  {
    ItemSizingStrategy = ItemSizingStrategy.MeasureAllItems,
  };
  private readonly HybridPaginationControl pendingReportsPaginationControl = new()
  {
    PaginationId = "community-pending-reports",
  };
  private readonly VerticalStackLayout pendingReportsPanel = new() { Spacing = 8 };

  private void InitializePendingReportsPagination()
  {
    pendingReportsView.ItemTemplate = new DataTemplate(RowTemplate);
    pendingReportsView.RemainingItemsThreshold = 2;
    pendingReportsView.RemainingItemsThresholdReached += (_, _) =>
    {
      if (viewModel.CanAutomaticallyLoadPendingReports)
        pendingReportsPaginationControl.TryLoadAutomatically();
    };
    pendingReportsPaginationControl.LoadNextPageRequested += async (_, _) =>
    {
      await viewModel.LoadMorePendingReportsAsync().ConfigureAwait(true);
      RenderPendingReports();
    };
    pendingReportsPanel.Children.Add(pendingReportsView);
    pendingReportsPanel.Children.Add(pendingReportsPaginationControl);
  }

  private void RenderPendingReports()
  {
    pendingReportsPanel.IsVisible = section == CommunityDetailSurfaceSection.Moderation;
    if (!pendingReportsPanel.IsVisible) return;
    pendingReportsView.ItemsSource = viewModel.PendingReportRows;
    pendingReportsPaginationControl.HasMore = viewModel.HasMorePendingReports;
    pendingReportsPaginationControl.IsLoading = viewModel.IsLoadingMorePendingReports;
    pendingReportsPaginationControl.HasError = viewModel.HasPendingReportPaginationError;
  }

  private static bool AffectsPendingReports(string? propertyName) => propertyName is null or
      nameof(CommunityDetailViewModel.PendingReportRows) or
      nameof(CommunityDetailViewModel.HasMorePendingReports) or
      nameof(CommunityDetailViewModel.IsLoadingMorePendingReports) or
      nameof(CommunityDetailViewModel.HasPendingReportPaginationError);
}
