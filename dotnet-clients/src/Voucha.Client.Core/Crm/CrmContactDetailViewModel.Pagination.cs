using Voucha.Client.Core.Api;
using Voucha.Client.Core.Pagination;

namespace Voucha.Client.Core.Crm;

public sealed partial class CrmContactDetailViewModel
{
  private readonly CursorPaginationState<CrmEmailRow, string> emailPages = new(row => row.Id);
  private readonly CursorPaginationState<CrmNoteRow, string> notePages = new(row => row.Id);

  public bool HasMoreEmails => ContactId is not null && emailPages.HasLoadedPage && emailPages.HasMore;
  public bool HasMoreNotes => ContactId is not null && notePages.HasLoadedPage && notePages.HasMore;
  public bool IsLoadingEmailPage => emailPages.IsLoading;
  public bool IsLoadingNotePage => notePages.IsLoading;
  public bool HasEmailPaginationError => emailPages.LastError is not null;
  public bool HasNotePaginationError => notePages.LastError is not null;

  public Task LoadMoreEmailsAsync(CancellationToken cancellationToken = default) =>
      RunDetailContinuationAsync(() => LoadEmailPageAsync(cancellationToken));

  public Task LoadMoreNotesAsync(CancellationToken cancellationToken = default) =>
      RunDetailContinuationAsync(() => LoadNotePageAsync(cancellationToken));

  private async Task LoadEmailPageAsync(CancellationToken cancellationToken)
  {
    if (ContactId is not { } id) return;
    var request = emailPages.BeginNextPage();
    if (request is null) return;
    NotifyDetailPaginationChanged();
    try
    {
      var response = await service.FetchContactEmailsAsync(
          id, request.Cursor, cancellationToken: cancellationToken).ConfigureAwait(true);
      var rows = response.Results.Select(item => CrmEmailRow.FromMessage(item, localization));
      if (ContactId == id && emailPages.Complete(
          request, rows, response.PageInfo.EndCursor,
          response.PageInfo.HasNextPage || response.PageInfo.HasMore == true)) Emails = emailPages.Items;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    { emailPages.Cancel(request); throw; }
    catch (OperationCanceledException ex) { emailPages.Fail(request, ex.Message); }
    catch (Exception ex) { if (emailPages.Fail(request, ex.Message)) throw; }
    finally { NotifyDetailPaginationChanged(); }
  }

  private async Task LoadNotePageAsync(CancellationToken cancellationToken)
  {
    if (ContactId is not { } id) return;
    var request = notePages.BeginNextPage();
    if (request is null) return;
    NotifyDetailPaginationChanged();
    try
    {
      var response = await service.FetchContactNotesAsync(
          id, request.Cursor, cancellationToken: cancellationToken).ConfigureAwait(true);
      var rows = response.Results.Select(item => CrmNoteRow.FromNote(item, localization));
      if (ContactId == id && notePages.Complete(
          request, rows, response.PageInfo.EndCursor,
          response.PageInfo.HasNextPage || response.PageInfo.HasMore == true)) Notes = notePages.Items;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    { notePages.Cancel(request); throw; }
    catch (OperationCanceledException ex) { notePages.Fail(request, ex.Message); }
    catch (Exception ex) { if (notePages.Fail(request, ex.Message)) throw; }
    finally { NotifyDetailPaginationChanged(); }
  }

  private async Task RunDetailContinuationAsync(Func<Task> load)
  {
    ErrorMessage = null;
    try { await load().ConfigureAwait(true); }
    catch (OperationCanceledException) { }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      ErrorMessage = ex.Message;
    }
  }

  private void ApplyInitialEmailPage(CrmEmailListResponse response)
  {
    emailPages.Reset();
    var request = emailPages.BeginNextPage()!;
    emailPages.Complete(request, response.Results.Select(item => CrmEmailRow.FromMessage(item, localization)),
        response.PageInfo.EndCursor, response.PageInfo.HasNextPage || response.PageInfo.HasMore == true);
    Emails = emailPages.Items;
  }

  private void ApplyInitialNotePage(CrmNoteListResponse response)
  {
    notePages.Reset();
    var request = notePages.BeginNextPage()!;
    notePages.Complete(request, response.Results.Select(item => CrmNoteRow.FromNote(item, localization)),
        response.PageInfo.EndCursor, response.PageInfo.HasNextPage || response.PageInfo.HasMore == true);
    Notes = notePages.Items;
    NotifyDetailPaginationChanged();
  }

  private void SynchronizeDetailPages() { SynchronizeEmailPage(); SynchronizeNotePage(); }
  private void ResetDetailPagination()
  {
    emailPages.Reset(); notePages.Reset();
    NotifyDetailPaginationChanged();
  }
  private void SynchronizeEmailPage() => emailPages.ReplaceItems(Emails);
  private void SynchronizeNotePage() => notePages.ReplaceItems(Notes);
  private void NotifyDetailPaginationChanged()
  {
    OnPropertyChanged(nameof(HasMoreEmails)); OnPropertyChanged(nameof(HasMoreNotes));
    OnPropertyChanged(nameof(IsLoadingEmailPage)); OnPropertyChanged(nameof(IsLoadingNotePage));
    OnPropertyChanged(nameof(HasEmailPaginationError)); OnPropertyChanged(nameof(HasNotePaginationError));
  }
}
