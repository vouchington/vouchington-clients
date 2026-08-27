using Voucha.Client.Core.Api;
using Voucha.Client.Core.Pagination;

namespace Voucha.Client.Core.Crm;

public sealed partial class CrmContactsViewModel
{
  private readonly CursorPaginationState<CrmContactRow, string> contactPages = new(row => row.Id);

  public bool HasMoreContacts => contactPages.HasLoadedPage && contactPages.HasMore;
  public bool IsLoadingContactPage => contactPages.IsLoading;
  public bool HasContactPaginationError => contactPages.LastError is not null;
  public string? ContactPaginationErrorMessage => contactPages.LastError;

  public async Task LoadMoreContactsAsync(CancellationToken cancellationToken = default)
  {
    if (IsLoadingContactPage) return;
    ErrorMessage = null;
    try { await LoadContactsPageAsync(replace: false, cancellationToken).ConfigureAwait(true); }
    catch (OperationCanceledException) { }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      ErrorMessage = ex.Message;
    }
  }

  private async Task LoadContactsPageAsync(bool replace, CancellationToken cancellationToken)
  {
    if (replace) contactPages.Reset();
    var request = contactPages.BeginNextPage();
    if (request is null) return;
    NotifyContactPaginationChanged();
    try
    {
      var response = await service.FetchContactsAsync(
          new CrmContactsRequest(
              SearchQuery, StatusFilter, VerticalFilter, LinkedFilter, request.Cursor, 25),
          cancellationToken).ConfigureAwait(true);
      var rows = response.Results.Select(contact => CrmContactRow.FromContact(contact, localization));
      if (contactPages.Complete(
          request, rows, response.PageInfo.EndCursor,
          response.PageInfo.HasNextPage || response.PageInfo.HasMore == true)) Contacts = contactPages.Items;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      contactPages.Cancel(request);
      throw;
    }
    catch (OperationCanceledException ex)
    {
      if (!contactPages.Fail(request, ex.Message)) return;
      if (replace) throw new InvalidOperationException(ex.Message, ex);
    }
    catch (Exception ex)
    {
      if (!contactPages.Fail(request, ex.Message)) return;
      throw;
    }
    finally { NotifyContactPaginationChanged(); }
  }

  private void SynchronizeContactPage() => contactPages.ReplaceItems(Contacts);

  private void NotifyContactPaginationChanged()
  {
    OnPropertyChanged(nameof(HasMoreContacts));
    OnPropertyChanged(nameof(IsLoadingContactPage));
    OnPropertyChanged(nameof(HasContactPaginationError));
    OnPropertyChanged(nameof(ContactPaginationErrorMessage));
  }
}
