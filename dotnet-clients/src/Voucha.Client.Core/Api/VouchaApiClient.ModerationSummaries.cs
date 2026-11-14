namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<StaffModerationAppealListResponse> FetchStaffAppealsAsync(
      ModerationAppealStatus status, string? after = null, int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<StaffModerationAppealListResponse>(
          VouchaApiEndpoints.Appeals(status, limit, after, mine: false), cancellationToken);

  public Task<StaffModerationDisputeListResponse> FetchStaffDisputesAsync(
      ModerationDisputeStatus status, string? after = null, int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<StaffModerationDisputeListResponse>(
          VouchaApiEndpoints.Disputes(status switch
          {
            ModerationDisputeStatus.Pending => "pending",
            ModerationDisputeStatus.Resolved => "resolved",
            ModerationDisputeStatus.Dismissed => "dismissed",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
          }, limit, after, mine: false),
          cancellationToken);
}
