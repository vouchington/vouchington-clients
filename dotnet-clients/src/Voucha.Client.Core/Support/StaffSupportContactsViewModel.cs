using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Support;

public sealed class StaffSupportContactsViewModel(
    IStaffSupportService service,
    IUiLocalization? localization = null) : ObservableObject
{
  private readonly IUiLocalization localization = localization ?? UiLocalization.English;
  private IReadOnlyList<SupportContact> contacts = [];
  private PageInfo? pageInfo;
  private string? query;
  private string? pageQuery;
  private CancellationTokenSource? activeLoadCancellation;
  private int loadGeneration;
  private bool isLoading;
  private string? errorMessage;

  public IReadOnlyList<SupportContact> Contacts { get => contacts; private set => SetProperty(ref contacts, value); }
  public string? Query { get => query; set { if (SetProperty(ref query, value)) SupersedeActiveLoad(); } }
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
    IsLoading = true;
    ErrorMessage = null;
    try
    {
      var response = await service.FetchContactsAsync(requestedQuery, after, 30, requestCancellation.Token).ConfigureAwait(true);
      if (!IsCurrent(requestGeneration, requestCancellation)) return;
      var existingIds = Contacts.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
      Contacts = replace ? response.Results : [.. Contacts, .. response.Results.Where(item => existingIds.Add(item.Id))];
      pageInfo = response.PageInfo;
      pageQuery = requestedQuery;
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

  private bool MatchesCurrentControls => string.Equals(pageQuery, Query?.Trim(), StringComparison.Ordinal);
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
