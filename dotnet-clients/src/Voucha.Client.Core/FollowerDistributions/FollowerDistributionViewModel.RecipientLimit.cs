namespace Voucha.Client.Core.FollowerDistributions;

public sealed partial class FollowerDistributionViewModel
{
  /// <summary>The server-configured selection cap; null until fetched, so only server validation applies.</summary>
  public int? RecipientLimit { get => recipientLimit; private set => SetProperty(ref recipientLimit, value); }

  public bool CanSelectMore => RecipientLimit is not { } limit || selectedIds.Count < limit;

  public async Task LoadRecipientLimitAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      var limits = await client.GetContentLimitsAsync(cancellationToken).ConfigureAwait(false);
      RecipientLimit = limits.FollowerDistributionRecipientsMax;
    }
    catch (HttpRequestException) { RecipientLimit = null; }
    OnPropertyChanged(nameof(CanSelectMore));
  }
}
