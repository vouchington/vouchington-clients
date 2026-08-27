using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.ReferralLinks;

public sealed partial class ReferralLinksViewModel
{
  public Task LoadMineAsync(CancellationToken cancellationToken = default) =>
      LoadMinePageAsync(after: null, append: false, cancellationToken);

  public Task LoadMoreMineAsync(CancellationToken cancellationToken = default) =>
      !IsLoading && HasMoreLinks && LinksEndCursor is not null
          ? LoadMinePageAsync(LinksEndCursor, append: true, cancellationToken)
          : Task.CompletedTask;

  private async Task LoadMinePageAsync(
      string? after,
      bool append,
      CancellationToken cancellationToken)
  {
    var currentRequest = BeginLoad();
    try
    {
      var response = await referralLinksService
          .FetchMineAsync(new FetchReferralLinksRequest(after), cancellationToken)
          .ConfigureAwait(true);
      CompleteManagedReferralRows(
          currentRequest,
          response.Results.Select(RowFromMine).ToArray(),
          response.PageInfo,
          append);
    }
    catch (OperationCanceledException)
    {
      CompleteCanceled(currentRequest);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (append)
      {
        CompleteErrorPreservingRows(currentRequest, ex.Message);
      }
      else
      {
        CompleteError(currentRequest, ex.Message);
      }
    }
  }
}
