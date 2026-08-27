namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<PointValuationsResponse> FetchPointValuationsAsync(
      string? after = null, int limit = 25, CancellationToken cancellationToken = default) =>
      SendAsync<PointValuationsResponse>(VouchaApiEndpoints.PointValuations(after, limit), cancellationToken);

  public Task<PointValuationResponse> CreatePointValuationAsync(
      CreatePointValuationBody body, CancellationToken cancellationToken = default) =>
      SendAsync<PointValuationResponse>(VouchaApiEndpoints.CreatePointValuation(body), cancellationToken);

  public Task<PointValuationResponse> UpdatePointValuationAsync(
      string id, UpdatePointValuationBody body, CancellationToken cancellationToken = default) =>
      SendAsync<PointValuationResponse>(VouchaApiEndpoints.UpdatePointValuation(id, body), cancellationToken);

  public Task DeletePointValuationAsync(string id, CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.DeletePointValuation(id), cancellationToken);

  public Task<TopicSearchResponse> SearchRewardsProgramTopicsAsync(
      string query, int limit = 10, CancellationToken cancellationToken = default) =>
      SendAsync<TopicSearchResponse>(VouchaApiEndpoints.RewardsProgramTopics(query, limit), cancellationToken);
}
