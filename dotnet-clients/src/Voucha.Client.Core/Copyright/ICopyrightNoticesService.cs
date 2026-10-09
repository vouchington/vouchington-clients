using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Copyright;

public interface ICopyrightNoticesService
{
  Task<CopyrightNoticesResponse> FetchPageAsync(string? after, int limit, CancellationToken cancellationToken);
  Task<CopyrightNoticeResponse> FetchDetailAsync(string id, CancellationToken cancellationToken);
  Task<CopyrightParticipantNoticeResponse> FetchParticipantAsync(string id, CancellationToken cancellationToken);
  Task<CopyrightEuDisputeSettlementsResponse> FetchSettlementsAsync(string id, string after, int limit, CancellationToken cancellationToken);
}

public sealed class ApiCopyrightNoticesService(VouchaApiClient client) : ICopyrightNoticesService
{
  public Task<CopyrightEuDisputeSettlementsResponse> FetchSettlementsAsync(string id, string after, int limit, CancellationToken cancellationToken) =>
      client.FetchCopyrightEuDisputeSettlementsAsync(id, after, limit, cancellationToken);
  public Task<CopyrightNoticesResponse> FetchPageAsync(string? after, int limit, CancellationToken cancellationToken) =>
      client.FetchCopyrightNoticesAsync(after, limit, cancellationToken);
  public Task<CopyrightNoticeResponse> FetchDetailAsync(string id, CancellationToken cancellationToken) =>
      client.FetchCopyrightNoticeAsync(id, cancellationToken);
  public Task<CopyrightParticipantNoticeResponse> FetchParticipantAsync(string id, CancellationToken cancellationToken) =>
      client.FetchCopyrightParticipantNoticeAsync(id, cancellationToken);
}
