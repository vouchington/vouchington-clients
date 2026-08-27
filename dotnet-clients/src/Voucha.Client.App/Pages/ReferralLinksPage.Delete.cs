using Voucha.Client.Core.Localization;
using Voucha.Client.Core.ReferralLinks;

namespace Voucha.Client.App.Pages;

public partial class ReferralLinksPage
{
  private async void OnDeleteClicked(object? sender, EventArgs e)
  {
    await RunPageActionAsync(async () =>
    {
      if (RowFromSender(sender) is not { } row) return;
      if (!await DisplayAlertAsync(
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsDeleteReferralLink),
          row.Title,
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDelete),
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpCancel))) return;
      await viewModel.DeleteAsync(row.Id);
      mode = ReferralLinksMode.Mine;
    });
  }

  private void ConfigurePagination()
  {
    LinksPaginationControl.LoadNextPageRequested += OnLoadMoreMineRequested;
    AnalyticsPaginationControl.LoadNextPageRequested += OnLoadMoreAnalyticsRequested;
  }

  private async void OnLoadMoreMineRequested(object? sender, EventArgs e) =>
      await RunPageActionAsync(() => viewModel.LoadMoreMineAsync());

  private async void OnLoadMoreAnalyticsRequested(object? sender, EventArgs e) =>
      await RunPageActionAsync(() => viewModel.LoadMoreAnalyticsAsync());

}
