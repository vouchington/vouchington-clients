namespace Voucha.Client.Core.Copyright;

public sealed partial class CopyrightNoticesViewModel
{
  private bool isLoadingSettlements;
  private bool hasSettlementError;
  public bool IsLoadingSettlements
  {
    get => isLoadingSettlements;
    private set => SetProperty(ref isLoadingSettlements, value);
  }
  public bool HasSettlementError
  {
    get => hasSettlementError;
    private set => SetProperty(ref hasSettlementError, value);
  }

  public async Task LoadMoreSettlementsAsync(CancellationToken cancellationToken = default)
  {
    var snapshot = SelectedCase;
    if (!IsSignedIn || IsLoadingSettlements || snapshot?.Participant?.Eu is not { } eu ||
        !eu.DisputeSettlementsPageInfo.HasNextPage || eu.DisputeSettlementsPageInfo.EndCursor is not { } cursor) return;
    IsLoadingSettlements = true;
    HasSettlementError = false;
    try
    {
      var page = await service.FetchSettlementsAsync(snapshot.Notice.Id, cursor, 25, cancellationToken).ConfigureAwait(true);
      cancellationToken.ThrowIfCancellationRequested();
      if (!ReferenceEquals(snapshot, SelectedCase)) return;
      var nextEu = eu with
      {
        DisputeSettlements = eu.DisputeSettlements.Concat(page.CopyrightEuDisputeSettlements).DistinctBy(item => item.Id).ToArray(),
        DisputeSettlementsPageInfo = page.PageInfo,
      };
      SelectedCase = CopyrightCaseSnapshot.FromParticipant(snapshot.Participant with { Eu = nextEu });
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    catch (HttpRequestException)
    {
      if (!cancellationToken.IsCancellationRequested && ReferenceEquals(snapshot, SelectedCase))
        HasSettlementError = true;
    }
    finally { IsLoadingSettlements = false; }
  }
}
