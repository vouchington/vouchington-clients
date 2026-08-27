using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Support;

public sealed class StaffSupportThreadsViewModel(
    IStaffSupportService service,
    IUiLocalization? localization = null) : ObservableObject
{
  private readonly IUiLocalization localization = localization ?? UiLocalization.English;
  private IReadOnlyList<SupportThread> threads = [];
  private PageInfo? pageInfo;
  private string? query;
  private StaffSupportThreadStatusFilter? status;
  private string? pageQuery;
  private StaffSupportThreadStatusFilter? pageStatus;
  private CancellationTokenSource? activeLoadCancellation;
  private int loadGeneration;
  private bool isLoading;
  private string? errorMessage;

  public IReadOnlyList<SupportThread> Threads { get => threads; private set => SetProperty(ref threads, value); }
  public string? Query { get => query; set { if (SetProperty(ref query, value)) SupersedeActiveLoad(); } }
  public StaffSupportThreadStatusFilter? Status { get => status; set { if (SetProperty(ref status, value)) SupersedeActiveLoad(); } }
  public bool IsLoading { get => isLoading; private set => SetProperty(ref isLoading, value); }
  public string? ErrorMessage { get => errorMessage; private set => SetProperty(ref errorMessage, value); }
  public bool HasMore => MatchesCurrentControls && pageInfo?.HasNextPage == true;

  public void ReportUnexpectedError(Exception exception)
  {
    ArgumentNullException.ThrowIfNull(exception);
    ErrorMessage = localization.Localize(UiMessageKey.NativeDotnetCsharpError);
  }

  public Task LoadAsync(CancellationToken cancellationToken = default) => LoadPageAsync(null, true, cancellationToken);
  public Task LoadMoreAsync(CancellationToken cancellationToken = default) =>
      !IsLoading && MatchesCurrentControls && pageInfo?.EndCursor is { } cursor
          ? LoadPageAsync(cursor, false, cancellationToken) : Task.CompletedTask;

  private async Task LoadPageAsync(string? after, bool replace, CancellationToken cancellationToken)
  {
    if (!replace && IsLoading) return;
    var requestGeneration = Interlocked.Increment(ref loadGeneration);
    var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    var previousCancellation = Interlocked.Exchange(ref activeLoadCancellation, requestCancellation);
    if (previousCancellation is not null)
    {
      try { await previousCancellation.CancelAsync().ConfigureAwait(true); }
      catch (ObjectDisposedException) { }
    }
    var requestedQuery = Query?.Trim();
    var requestedStatus = Status;
    IsLoading = true;
    ErrorMessage = null;
    try
    {
      var response = await service.FetchThreadsAsync(requestedQuery, requestedStatus, after, 30, requestCancellation.Token).ConfigureAwait(true);
      if (!IsCurrent(requestGeneration, requestCancellation)) return;
      Threads = replace ? response.Results : [.. Threads, .. response.Results.Where(item => Threads.All(existing => existing.Id != item.Id))];
      pageInfo = response.PageInfo;
      pageQuery = requestedQuery;
      pageStatus = requestedStatus;
      OnPropertyChanged(nameof(HasMore));
    }
    catch (OperationCanceledException) when (requestCancellation.IsCancellationRequested) { }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (IsCurrent(requestGeneration, requestCancellation)) ErrorMessage = ex.Message;
    }
    finally
    {
      if (IsCurrent(requestGeneration, requestCancellation)) IsLoading = false;
      Interlocked.CompareExchange(ref activeLoadCancellation, null, requestCancellation);
      requestCancellation.Dispose();
    }
  }

  private bool MatchesCurrentControls => string.Equals(pageQuery, Query?.Trim(), StringComparison.Ordinal) && pageStatus == Status;
  private bool IsCurrent(int requestGeneration, CancellationTokenSource requestCancellation) =>
      requestGeneration == Volatile.Read(ref loadGeneration) && ReferenceEquals(activeLoadCancellation, requestCancellation);
  private void SupersedeActiveLoad()
  {
    Interlocked.Increment(ref loadGeneration);
    var cancellation = Interlocked.Exchange(ref activeLoadCancellation, null);
    if (cancellation is not null) _ = CancelAsync(cancellation);
    IsLoading = false;
    OnPropertyChanged(nameof(HasMore));
  }
  private static async Task CancelAsync(CancellationTokenSource cancellation)
  {
    try { await cancellation.CancelAsync().ConfigureAwait(false); }
    catch (ObjectDisposedException) { }
  }
}
