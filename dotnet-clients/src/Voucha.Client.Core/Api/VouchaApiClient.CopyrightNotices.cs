namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<CopyrightEuDisputeSettlementsResponse> FetchCopyrightEuDisputeSettlementsAsync(
      string id, string? after = null, int? limit = null, CancellationToken cancellationToken = default) =>
      SendAsync<CopyrightEuDisputeSettlementsResponse>(VouchaApiEndpoints.CopyrightEuDisputeSettlements(id, after, limit), cancellationToken);

  public Task<CopyrightNoticesResponse> FetchCopyrightNoticesAsync(
      string? after = null, int? limit = null, CancellationToken cancellationToken = default) =>
      SendAsync<CopyrightNoticesResponse>(VouchaApiEndpoints.CopyrightNotices(after, limit), cancellationToken);

  public Task<CopyrightNoticeResponse> FetchCopyrightNoticeAsync(
      string id, CancellationToken cancellationToken = default) =>
      SendAsync<CopyrightNoticeResponse>(VouchaApiEndpoints.CopyrightNotice(id), cancellationToken);

  public Task<CopyrightParticipantNoticeResponse> FetchCopyrightParticipantNoticeAsync(
      string id, CancellationToken cancellationToken = default) =>
      SendAsync<CopyrightParticipantNoticeResponse>(VouchaApiEndpoints.CopyrightParticipantNotice(id), cancellationToken);
}
