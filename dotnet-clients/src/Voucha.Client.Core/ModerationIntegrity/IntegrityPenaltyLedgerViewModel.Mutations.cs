using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.ModerationIntegrity;

public abstract partial class IntegrityPenaltyLedgerViewModel<TPenalty>
{
  public async Task RevokeAsync(string penaltyId, CancellationToken cancellationToken = default)
  {
    if (!CanRevoke(penaltyId) || !actionIds.Add(penaltyId)) return;
    actionErrors.Remove(penaltyId);
    NotifyActions();
    try
    {
      var penalty = await RevokeExactAsync(penaltyId, cancellationToken).ConfigureAwait(true);
      AcceptAuthoritative(penalty);
      reconciliationIds.Remove(penaltyId);
    }
    catch (Exception exception) when (IntegrityMutationFailure.IsAmbiguous(exception))
    {
      reconciliationIds.Add(penaltyId);
      actionErrors[penaltyId] = UiText.Localized(UiMessageKey.NativeSwiftIntegrityResultUncertain);
      await ReconcileCoreAsync(penaltyId).ConfigureAwait(true);
    }
    catch (Exception exception) when (IntegrityMutationFailure.IsExpected(exception))
    {
      actionErrors[penaltyId] = UiText.ExternalContent(exception.Message);
    }
    finally
    {
      actionIds.Remove(penaltyId);
      NotifyActions();
    }
  }

  public async Task ReconcileAsync(string penaltyId)
  {
    if (!NeedsReconciliation(penaltyId) || !actionIds.Add(penaltyId)) return;
    NotifyActions();
    try { await ReconcileCoreAsync(penaltyId).ConfigureAwait(true); }
    finally
    {
      actionIds.Remove(penaltyId);
      NotifyActions();
    }
  }

  private async Task ReconcileCoreAsync(string penaltyId)
  {
    try
    {
      var penalty = await FetchExactAsync(penaltyId, CancellationToken.None).ConfigureAwait(true);
      AcceptAuthoritative(penalty);
      reconciliationIds.Remove(penaltyId);
      actionErrors.Remove(penaltyId);
    }
    catch (Exception exception) when (IntegrityMutationFailure.IsExpected(exception) ||
        exception is OperationCanceledException)
    {
      actionErrors[penaltyId] = UiText.Localized(UiMessageKey.NativeSwiftIntegrityReconciliationFailed);
    }
  }

  private void AcceptAuthoritative(TPenalty penalty)
  {
    var id = Id(penalty);
    if (Status == IntegrityPenaltyStatus.Active && IsRevoked(penalty))
      Items = Items.Where(item => Id(item) != id).ToArray();
    else
      Items = Items.Select(item => Id(item) == id ? penalty : item).ToArray();
  }

  private void NotifyActions()
  {
    OnPropertyChanged(nameof(Items));
    OnPropertyChanged(nameof(ErrorMessage));
  }
}
