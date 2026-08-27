using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;

namespace Voucha.Client.App.Pages;

public partial class ReviewQueuePage : ContentPage
{
  private readonly ReviewQueueViewModel viewModel;
  private CancellationTokenSource? pageCancellation;

  public ReviewQueuePage(ReviewQueueViewModel viewModel)
  {
    InitializeComponent();
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    BindingContext = viewModel;
    PaginationControl.LoadNextPageRequested += OnLoadNextPageRequested;
  }

  public Task ReloadAsync() => viewModel.ReloadAsync(CurrentPageToken());

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    StartPageOperations();
    await RunPageOperationAsync(viewModel.ResumeAsync).ConfigureAwait(true);
  }

  protected override void OnDisappearing()
  {
    CancelPageOperations();
    viewModel.CancelListOperations();
    viewModel.CancelExposureOperations();
    base.OnDisappearing();
  }

  private async void OnRefreshing(object? sender, EventArgs e)
  {
    await RunPageOperationAsync(viewModel.RefreshAsync).ConfigureAwait(true);
    if (sender is RefreshView refreshView) refreshView.IsRefreshing = false;
  }

  private void OnRemainingItemsThresholdReached(object? sender, EventArgs e) =>
      PaginationControl.TryLoadAutomatically();

  private async void OnLoadNextPageRequested(object? sender, EventArgs e) =>
      await RunPageOperationAsync(viewModel.LoadMoreAsync).ConfigureAwait(true);

  private async void OnApproveClicked(object? sender, EventArgs e) =>
      await RunPageOperationAsync(
          cancellationToken => PerformAsync(sender, PostClearanceAction.Approved, cancellationToken)).ConfigureAwait(true);

  private async void OnRejectClicked(object? sender, EventArgs e) =>
      await RunPageOperationAsync(
          cancellationToken => PerformAsync(sender, PostClearanceAction.Rejected, cancellationToken)).ConfigureAwait(true);

  private async void OnReReviewClicked(object? sender, EventArgs e) =>
      await RunPageOperationAsync(
          cancellationToken => PerformAsync(sender, PostClearanceAction.InReview, cancellationToken)).ConfigureAwait(true);

  private async void OnRevealClicked(object? sender, EventArgs e) =>
      await RunPageOperationAsync(
          cancellationToken => RevealAsync(sender, cancellationToken)).ConfigureAwait(true);

  private async void OnCheckExposureClicked(object? sender, EventArgs e) =>
      await RunPageOperationAsync(viewModel.RefreshExposureAsync).ConfigureAwait(true);

  private Task PerformAsync(object? sender, PostClearanceAction action, CancellationToken cancellationToken) =>
      sender is Button { CommandParameter: ReviewQueueRow row }
          ? viewModel.PerformAsync(row, action, cancellationToken)
          : Task.CompletedTask;

  private Task RevealAsync(object? sender, CancellationToken cancellationToken) =>
      sender is Button { CommandParameter: ReviewQueueRow row }
          ? viewModel.RevealMediaAsync(row, cancellationToken)
          : Task.CompletedTask;

  private CancellationToken StartPageOperations()
  {
    CancelPageOperations();
    pageCancellation = new CancellationTokenSource();
    return pageCancellation.Token;
  }

  private void CancelPageOperations()
  {
    pageCancellation?.Cancel();
    pageCancellation?.Dispose();
    pageCancellation = null;
  }

  private CancellationToken CurrentPageToken() => pageCancellation?.Token ?? StartPageOperations();

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "MAUI async event handlers must not propagate unexpected exceptions.")]
  private async Task RunPageOperationAsync(Func<CancellationToken, Task> operation)
  {
    try
    {
      await operation(CurrentPageToken()).ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
    }
    catch (Exception ex)
    {
      Debug.WriteLine(ex);
    }
  }
}
