using Voucha.Client.Core.Api;
using Voucha.Client.Core.Pagination;

namespace Voucha.Client.Core.Settings;

public sealed partial class EmailAddressManagerViewModel
{
  private readonly CursorPaginationState<EmailAddress, string> emailPages = new(item => item.Address);

  public bool HasMoreEmailAddresses => emailPages.HasLoadedPage && emailPages.HasMore;
  public bool IsLoadingMoreEmailAddresses => IsBusy || emailPages.IsLoading;
  public bool HasEmailAddressPaginationError => emailPages.LastError is not null;

  public async Task LoadMoreEmailAddressesAsync(CancellationToken cancellationToken = default)
  {
    if (IsBusy) return;
    var request = emailPages.BeginNextPage();
    if (request is null) return;
    NotifyEmailPagination();
    try
    {
      var response = await service.FetchEmailAddressesPageAsync(
          request.Cursor,
          25,
          cancellationToken).ConfigureAwait(true);
      var pageInfo = response.PageInfo ?? new PageInfo(null, false, null);
      if (emailPages.Complete(
          request,
          response.Results,
          pageInfo.EndCursor,
          pageInfo.HasNextPage || pageInfo.HasMore == true)) EmailAddresses = emailPages.Items;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      emailPages.Cancel(request);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      emailPages.Fail(request, ex.Message);
      ErrorMessage = ex.Message;
    }
    finally
    {
      NotifyEmailPagination();
    }
  }

  private void ReplaceEmailPage(EmailAddressListResponse response)
  {
    var pageInfo = response.PageInfo ?? new PageInfo(null, false, null);
    emailPages.Reset(response.Results);
    emailPages.RestoreContinuation(
        pageInfo.EndCursor,
        pageInfo.HasNextPage || pageInfo.HasMore == true);
    EmailAddresses = emailPages.Items;
    NotifyEmailPagination();
  }

  private void NotifyEmailPagination()
  {
    OnPropertyChanged(nameof(HasMoreEmailAddresses));
    OnPropertyChanged(nameof(IsLoadingMoreEmailAddresses));
    OnPropertyChanged(nameof(HasEmailAddressPaginationError));
  }
}
