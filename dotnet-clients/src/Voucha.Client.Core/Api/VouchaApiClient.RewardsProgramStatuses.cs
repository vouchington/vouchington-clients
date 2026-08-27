namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<RewardsProgramStatusesResponse> FetchRewardsProgramStatusesAsync(
      string? after = null, int limit = 25, CancellationToken cancellationToken = default) =>
      SendAsync<RewardsProgramStatusesResponse>(VouchaApiEndpoints.RewardsProgramStatuses(after, limit), cancellationToken);

  public Task<RewardsProgramStatusResponse> CreateRewardsProgramStatusAsync(
      CreateRewardsProgramStatusBody body, CancellationToken cancellationToken = default) =>
      SendAsync<RewardsProgramStatusResponse>(VouchaApiEndpoints.CreateRewardsProgramStatus(body), cancellationToken);

  public Task<RewardsProgramStatusResponse> UpdateRewardsProgramStatusAsync(
      string id, UpdateRewardsProgramStatusBody body, CancellationToken cancellationToken = default) =>
      SendAsync<RewardsProgramStatusResponse>(VouchaApiEndpoints.UpdateRewardsProgramStatus(id, body), cancellationToken);

  public Task DeleteRewardsProgramStatusAsync(string id, CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.DeleteRewardsProgramStatus(id), cancellationToken);

  public Task<TopicSearchResponse> SearchRewardsProgramStatusTopicsAsync(
      string query, int limit = 10, CancellationToken cancellationToken = default) =>
      SendAsync<TopicSearchResponse>(VouchaApiEndpoints.RewardsProgramStatusTopics(query, limit), cancellationToken);
}
