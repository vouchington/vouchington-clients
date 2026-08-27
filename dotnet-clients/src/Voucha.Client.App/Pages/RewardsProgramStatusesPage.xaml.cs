using Voucha.Client.Core.Localization;
using Voucha.Client.Core.RewardsProgramStatuses;

namespace Voucha.Client.App.Pages;

public partial class RewardsProgramStatusesPage : ContentPage, IDisposable
{
  private readonly RewardsProgramStatusesViewModel viewModel;

  public RewardsProgramStatusesPage(RewardsProgramStatusesViewModel viewModel)
  {
    InitializeComponent();
    this.viewModel = viewModel;
    BindingContext = viewModel;
    Unloaded += (_, _) => Dispose();
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    await viewModel.EnsureLoadedAsync();
  }

  private async void OnSearchClicked(object? sender, EventArgs e) => await viewModel.SearchAsync();
  private async void OnRetrySearchClicked(object? sender, EventArgs e) => await viewModel.RetrySearchAsync();

  private async void OnCreateClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: RewardsProgramStatusOptionRow row })
      await viewModel.CreateAsync(row);
  }

  private void OnEditClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: RewardsProgramStatusRow row })
      viewModel.BeginEdit(row);
  }

  private async void OnSaveClicked(object? sender, EventArgs e) => await viewModel.SaveAsync();
  private void OnCancelClicked(object? sender, EventArgs e) => viewModel.CancelEdit();
  private async void OnRetryClicked(object? sender, EventArgs e) => await RetryAsync();
  private async void OnLoadMoreRequested(object? sender, EventArgs e) => await viewModel.LoadMoreAsync();

  private async void OnScrolled(object? sender, ScrolledEventArgs e)
  {
    if (sender is ScrollView scroll)
      await LoadMoreForScrollAsync(e.ScrollY, scroll.Height, scroll.ContentSize.Height);
  }

  internal Task LoadMoreForScrollAsync(
      double scrollY, double viewportHeight, double contentHeight, CancellationToken cancellationToken = default) =>
      viewModel.HasNextPage && !viewModel.HasContinuationError && !viewModel.IsLoading && !viewModel.IsLoadingMore &&
      scrollY + viewportHeight >= contentHeight - 80
          ? viewModel.LoadMoreAsync(cancellationToken)
          : Task.CompletedTask;

  internal Task RetryAsync(CancellationToken cancellationToken = default) =>
      viewModel.RetryAsync(cancellationToken);

  private async void OnDeleteClicked(object? sender, EventArgs e)
  {
    if (sender is not Button { CommandParameter: RewardsProgramStatusRow row }) return;
    if (await DisplayAlertAsync(
        UiCopy.Localize(UiMessageKey.ExtractedRewardsProgramStatusesManagerStatusSummaryRemove9fe2f243),
        row.LocalizedName,
        UiCopy.Localize(UiMessageKey.ExtractedRewardsProgramStatusesManagerStatusSummaryConfirmEebdd24a),
        UiCopy.Localize(UiMessageKey.ExtractedRewardsProgramStatusesManagerStatusSummaryCancel19766ed6)))
      await DeleteConfirmedAsync(row);
  }

  internal Task DeleteConfirmedAsync(RewardsProgramStatusRow row, CancellationToken cancellationToken = default) =>
      viewModel.DeleteAsync(row, cancellationToken);

  public void Dispose() => viewModel.Dispose();
}
