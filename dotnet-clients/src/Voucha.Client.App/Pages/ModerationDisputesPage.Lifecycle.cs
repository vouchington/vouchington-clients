namespace Voucha.Client.App.Pages;

public sealed partial class ModerationDisputesPage
{
  private readonly object loadSync = new();
  private CancellationTokenSource? loadCancellation;
  private CancellationTokenSource? mutationCancellation = new();
  private Task activeLoad = Task.CompletedTask;
  private int requestedLoadRevision;
  private bool disposed;

  public void Dispose()
  {
    if (disposed) return;
    disposed = true;
    CancelLifecycleOperations();
    statusPicker.SelectedIndexChanged -= StatusChangedAsync;
    pagination.LoadNextPageRequested -= LoadNextPageAsync;
    disputesView.RemainingItemsThresholdReached -= RemainingItemsThresholdReached;
    viewModel.PropertyChanged -= ViewModelPropertyChanged;
    viewModel.Dispose();
    BindingContext = null;
  }

  protected override async void OnAppearing()
  {
    mutationCancellation ??= new CancellationTokenSource();
    base.OnAppearing();
    await ReloadAsync().ConfigureAwait(true);
  }

  protected override void OnDisappearing()
  {
    CancelLifecycleOperations();
    base.OnDisappearing();
  }

  public Task ReloadAsync() =>
      RunLatestLoadAsync(viewModel.LoadAsync);

  internal void CancelLifecycleOperations()
  {
    lock (loadSync)
    {
      _ = Interlocked.Increment(ref requestedLoadRevision);
      loadCancellation?.Cancel();
    }
    CancelAndDispose(ref mutationCancellation);
  }

  private CancellationToken CurrentMutationToken() =>
      mutationCancellation?.Token ?? new CancellationToken(canceled: true);

  private async Task RunLatestLoadAsync(
      Func<CancellationToken, Task> operation)
  {
    var revision = Interlocked.Increment(ref requestedLoadRevision);
    Task previous;
    lock (loadSync)
    {
      loadCancellation?.Cancel();
      previous = activeLoad;
    }
    await previous.ConfigureAwait(true);

    Task current;
    lock (loadSync)
    {
      if (revision != Volatile.Read(ref requestedLoadRevision)) return;
      loadCancellation?.Dispose();
      loadCancellation = new CancellationTokenSource();
      current = operation(loadCancellation.Token);
      activeLoad = current;
    }
    try
    {
      await current.ConfigureAwait(true);
    }
    finally
    {
      lock (loadSync)
      {
        if (ReferenceEquals(activeLoad, current))
        {
          loadCancellation?.Dispose();
          loadCancellation = null;
          activeLoad = Task.CompletedTask;
        }
      }
    }
  }

  private static void CancelAndDispose(ref CancellationTokenSource? cancellation)
  {
    cancellation?.Cancel();
    cancellation?.Dispose();
    cancellation = null;
  }
}
