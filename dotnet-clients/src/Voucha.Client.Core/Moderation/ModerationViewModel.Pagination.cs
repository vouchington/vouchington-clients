using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Pagination;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Moderation;

public sealed partial class ModerationViewModel
{
  private readonly CursorPaginationState<ModerationRow, string> pages = new(row => row.Id);

  public bool HasMore => IsCursorPagedContext && pages.HasLoadedPage && pages.HasMore;
  public bool CanLoadMore => HasMore && !IsLoading && !pages.IsLoading;
  public bool IsLoadingMore => pages.IsLoading && State != LoadState.Loading;
  public string? PaginationErrorMessage => pages.LastError;
  public bool HasPaginationError => !string.IsNullOrWhiteSpace(PaginationErrorMessage);
  public string PaginationActionTitle => localization.Localize(
      HasPaginationError
          ? UiMessageKey.NativeDotnetEngineeringPaginationRetry
          : UiMessageKey.NativeSwiftCommonLoadMore);

  private bool IsCursorPagedContext => Context?.RouteKind is
      ModerationRouteKind.Appeals or
      ModerationRouteKind.Disputes or
      ModerationRouteKind.AdminModlog or
      ModerationRouteKind.Transparency || IsPagedPersonalCases;

  private bool IsPagedPersonalCases => Context is
  {
    RouteKind: ModerationRouteKind.PersonalCases,
    ApiPath: "/api/v1/my/bans" or "/api/v1/my/removed-posts",
  };

  public Task LoadMoreAsync(CancellationToken cancellationToken = default) =>
      LoadModerationPageAsync(replace: false, cancellationToken);

  private async Task LoadModerationPageAsync(bool replace, CancellationToken cancellationToken)
  {
    if (Context is null) return;
    if (replace) pages.Reset();

    if (!IsCursorPagedContext)
    {
      if (!replace) return;
      await LoadStaticModerationSurfaceAsync(cancellationToken).ConfigureAwait(true);
      NotifyPaginationChanged();
      return;
    }

    var request = replace ? pages.BeginInitialPageIfNeeded() : pages.BeginNextPage();
    if (request is null) return;
    if (replace)
    {
      State = LoadState.Loading;
      ErrorMessage = null;
    }
    NotifyPaginationChanged();

    try
    {
      var page = Context.RouteKind switch
      {
        ModerationRouteKind.Appeals => await LoadAppealsAsync(request.Cursor, cancellationToken).ConfigureAwait(true),
        ModerationRouteKind.Disputes => await LoadDisputesAsync(request.Cursor, cancellationToken).ConfigureAwait(true),
        ModerationRouteKind.AdminModlog => await LoadModlogAsync(request.Cursor, cancellationToken).ConfigureAwait(true),
        ModerationRouteKind.Transparency => await LoadTransparencyPageAsync(request.Cursor, cancellationToken).ConfigureAwait(true),
        ModerationRouteKind.PersonalCases => await LoadPersonalCasesPageAsync(request.Cursor, cancellationToken).ConfigureAwait(true),
        _ => throw new InvalidOperationException($"Unsupported paged moderation route kind: {Context.RouteKind}"),
      };
      var completed = page.ReplaceExistingRows
          ? pages.CompleteReplacing(
              request,
              page.Rows,
              page.PageInfo.EndCursor,
              page.PageInfo.HasNextPage || page.PageInfo.HasMore == true)
          : pages.Complete(
              request,
              page.Rows,
              page.PageInfo.EndCursor,
              page.PageInfo.HasNextPage || page.PageInfo.HasMore == true);
      if (!completed) return;
      if (page.TransparencyBuckets is not null)
      {
        transparencyBuckets = page.TransparencyBuckets;
      }
      Items = pages.Items;
      State = LoadState.Loaded;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      if (!pages.Cancel(request)) return;
      if (replace) State = LoadState.Idle;
    }
    catch (OperationCanceledException ex)
    {
      if (!pages.Fail(request, ex.Message)) return;
      if (replace)
      {
        Items = [];
        ErrorMessage = ex.Message;
        State = LoadState.Error;
      }
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (!pages.Fail(request, ex.Message)) return;
      if (replace)
      {
        Items = [];
        ErrorMessage = ex.Message;
        State = LoadState.Error;
      }
    }
    finally
    {
      NotifyPaginationChanged();
    }
  }

  private async Task LoadStaticModerationSurfaceAsync(CancellationToken cancellationToken)
  {
    State = LoadState.Loading;
    ErrorMessage = null;
    Items = [];
    try
    {
      Items = Context!.RouteKind switch
      {
        ModerationRouteKind.Reports => throw new InvalidOperationException("Reports use the dedicated report triage surface."),
        ModerationRouteKind.AdminAnalytics => await LoadAnalyticsAsync(cancellationToken).ConfigureAwait(true),
        ModerationRouteKind.PersonalCases => await LoadPersonalCasesAsync(cancellationToken).ConfigureAwait(true),
        _ => throw new InvalidOperationException($"Unsupported moderation route kind: {Context.RouteKind}"),
      };
      State = LoadState.Loaded;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      Items = [];
      ErrorMessage = ex.Message;
      State = LoadState.Error;
    }
  }

  private void ResetPagination()
  {
    pages.Reset();
    Items = [];
    State = LoadState.Idle;
    ErrorMessage = null;
    NotifyPaginationChanged();
  }

  private void NotifyPaginationChanged()
  {
    OnPropertyChanged(nameof(HasMore));
    OnPropertyChanged(nameof(CanLoadMore));
    OnPropertyChanged(nameof(IsLoadingMore));
    OnPropertyChanged(nameof(PaginationErrorMessage));
    OnPropertyChanged(nameof(HasPaginationError));
    OnPropertyChanged(nameof(PaginationActionTitle));
  }

  private sealed record ModerationCursorPage(
      IReadOnlyList<ModerationRow> Rows,
      PageInfo PageInfo,
      IReadOnlyList<ModerationTransparencyBucket>? TransparencyBuckets = null,
      bool ReplaceExistingRows = false);
}
