using Voucha.Client.Core.Api;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Copyright;

public sealed partial class CopyrightNoticesViewModel(
    ICopyrightNoticesService service, NavigationViewer viewer) : ObservableObject
{
  private IReadOnlyList<CopyrightNoticeSummary> notices = [];
  private bool isLoading;
  private bool hasError;
  private string? nextCursor;
  private bool hasMore;
  private CopyrightCaseSnapshot? selectedCase;
  public bool IsSignedIn => viewer.IsAuthenticated;
  public IReadOnlyList<CopyrightNoticeSummary> Notices
  {
    get => notices;
    private set => SetProperty(ref notices, value);
  }
  public bool IsLoading
  {
    get => isLoading;
    private set => SetProperty(ref isLoading, value);
  }
  public bool HasError
  {
    get => hasError;
    private set => SetProperty(ref hasError, value);
  }
  public bool HasMore
  {
    get => hasMore;
    private set => SetProperty(ref hasMore, value);
  }
  public CopyrightCaseSnapshot? SelectedCase
  {
    get => selectedCase;
    private set => SetProperty(ref selectedCase, value);
  }

  public Task LoadAsync(CancellationToken cancellationToken = default) =>
      LoadPageAsync(null, replace: true, cancellationToken);

  public Task LoadMoreAsync(CancellationToken cancellationToken = default) =>
      HasMore && nextCursor is not null
          ? LoadPageAsync(nextCursor, replace: false, cancellationToken)
          : Task.CompletedTask;

  private async Task LoadPageAsync(string? after, bool replace, CancellationToken cancellationToken)
  {
    if (!IsSignedIn || IsLoading) return;
    IsLoading = true;
    HasError = false;
    try
    {
      var page = await service.FetchPageAsync(after, 25, cancellationToken).ConfigureAwait(true);
      cancellationToken.ThrowIfCancellationRequested();
      Notices = (replace ? page.CopyrightNotices : Notices.Concat(page.CopyrightNotices))
          .DistinctBy(notice => notice.Id).ToArray();
      nextCursor = page.PageInfo.EndCursor;
      HasMore = page.PageInfo.HasNextPage && nextCursor is not null;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    catch (HttpRequestException)
    {
      if (!cancellationToken.IsCancellationRequested) HasError = true;
    }
    finally { IsLoading = false; }
  }
}
