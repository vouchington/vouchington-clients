using System.Net;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Copyright;

public sealed partial class CopyrightNoticesViewModel
{
  public async Task LoadCaseAsync(string id, CancellationToken cancellationToken = default)
  {
    if (!IsSignedIn || IsLoading) return;
    IsLoading = true;
    HasError = false;
    HasSettlementError = false;
    SelectedCase = null;
    try
    {
      CopyrightParticipantNotice? participant = null;
      try
      {
        participant = (await service.FetchParticipantAsync(id, cancellationToken).ConfigureAwait(true)).CopyrightNotice;
      }
      catch (HttpRequestException exception) when (
          exception.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
      { }
      cancellationToken.ThrowIfCancellationRequested();
      if (participant is { Jurisdiction: "eu_dsa" })
        SelectedCase = CopyrightCaseSnapshot.FromParticipant(participant);
      else
      {
        var detail = await service.FetchDetailAsync(id, cancellationToken).ConfigureAwait(true);
        cancellationToken.ThrowIfCancellationRequested();
        SelectedCase = CopyrightCaseSnapshot.FromPublic(detail.CopyrightNotice, participant);
      }
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    catch (HttpRequestException)
    {
      if (!cancellationToken.IsCancellationRequested) HasError = true;
    }
    finally { IsLoading = false; }
  }
}
