using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.PointValuations;

namespace Voucha.Client.App.Pages;

public partial class PointValuationsPage : ContentPage, IDisposable
{
  private readonly PointValuationsViewModel viewModel;

  public PointValuationsPage(PointValuationsViewModel viewModel)
  {
    InitializeComponent();
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    BindingContext = viewModel;
    Unloaded += (_, _) => Dispose();
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The view model owns presentation failures.")]
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    try { await LoadForAppearanceAsync(); }
    catch (Exception error) { System.Diagnostics.Debug.WriteLine(error); }
  }

  internal Task LoadForAppearanceAsync(CancellationToken cancellationToken = default) =>
      viewModel.EnsureLoadedAsync(cancellationToken);

  private async void OnSearchClicked(object? sender, EventArgs eventArgs) => await viewModel.SearchAsync();
  private async void OnRetrySearchClicked(object? sender, EventArgs eventArgs) => await viewModel.RetrySearchAsync();

  private async void OnCreateClicked(object? sender, EventArgs eventArgs)
  {
    if (sender is Button { CommandParameter: RewardsProgramRow row }) await viewModel.CreateAsync(row);
  }

  private void OnEditClicked(object? sender, EventArgs eventArgs)
  {
    if (sender is Button { CommandParameter: PointValuationRow row }) viewModel.BeginEdit(row);
  }

  private async void OnSaveClicked(object? sender, EventArgs eventArgs) => await viewModel.SaveAsync();
  private void OnCancelClicked(object? sender, EventArgs eventArgs) => viewModel.CancelEdit();
  private async void OnRetryClicked(object? sender, EventArgs eventArgs) => await RetryAsync();
  private async void OnLoadMoreRequested(object? sender, EventArgs eventArgs) => await viewModel.LoadMoreAsync();

  private async void OnScrolled(object? sender, ScrolledEventArgs eventArgs)
  {
    if (sender is ScrollView scroll)
      await LoadMoreForScrollAsync(eventArgs.ScrollY, scroll.Height, scroll.ContentSize.Height);
  }

  internal Task LoadMoreForScrollAsync(
      double scrollY, double viewportHeight, double contentHeight, CancellationToken cancellationToken = default) =>
      viewModel.HasNextPage && !viewModel.HasContinuationError && !viewModel.IsLoading && !viewModel.IsLoadingMore &&
      scrollY + viewportHeight >= contentHeight - 80
          ? viewModel.LoadMoreAsync(cancellationToken)
          : Task.CompletedTask;

  internal Task RetryAsync(CancellationToken cancellationToken = default) =>
      viewModel.RetryAsync(cancellationToken);

  private async void OnDeleteClicked(object? sender, EventArgs eventArgs)
  {
    if (sender is not Button { CommandParameter: PointValuationRow row }) return;
    var confirmed = await DisplayAlertAsync(
        UiCopy.Localize(UiMessageKey.ExtractedPointValuationsManagerValuationSummaryRemove9fe2f243),
        row.LocalizedName,
        UiCopy.Localize(UiMessageKey.ExtractedPointValuationsManagerValuationSummaryConfirmEebdd24a),
        UiCopy.Localize(UiMessageKey.ExtractedPointValuationsManagerValuationSummaryCancel19766ed6));
    if (confirmed) await DeleteConfirmedAsync(row);
  }

  internal Task DeleteConfirmedAsync(PointValuationRow row, CancellationToken cancellationToken = default) =>
      viewModel.DeleteAsync(row, cancellationToken);

  public void Dispose() => viewModel.Dispose();
}
