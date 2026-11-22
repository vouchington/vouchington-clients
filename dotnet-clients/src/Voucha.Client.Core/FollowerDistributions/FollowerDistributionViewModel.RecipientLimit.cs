namespace Voucha.Client.Core.FollowerDistributions;

public sealed partial class FollowerDistributionViewModel
{
  /// <summary>The server-configured selection cap; null until fetched, so only server validation applies.</summary>
  public int? RecipientLimit { get => recipientLimit; private set => SetProperty(ref recipientLimit, value); }

  public bool CanSelectMore => RecipientLimit is not { } limit || selectedIds.Count < limit;

  /// A selection made before the cap loaded can exceed it; Submit stays disabled until it fits.
  private bool SelectionFitsRecipientLimit => RecipientLimit is not { } limit || selectedIds.Count <= limit;

  /// Reset bumps this, so a response from an earlier sheet cannot overwrite the current cap.
  private int recipientLimitGeneration;

  public async Task LoadRecipientLimitAsync(CancellationToken cancellationToken = default)
  {
    var generation = recipientLimitGeneration;
    int? limit;
    try
    {
      var limits = await client.GetContentLimitsAsync(cancellationToken).ConfigureAwait(false);
      limit = limits.FollowerDistributionRecipientsMax;
    }
    catch (HttpRequestException) { limit = null; }
    if (generation != recipientLimitGeneration) return;
    RecipientLimit = limit;
    OnPropertyChanged(nameof(CanSelectMore));
    OnPropertyChanged(nameof(CanSubmit));
  }
}
