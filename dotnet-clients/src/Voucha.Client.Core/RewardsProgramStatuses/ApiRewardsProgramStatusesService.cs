using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.RewardsProgramStatuses;

public sealed class ApiRewardsProgramStatusesService(VouchaApiClient client) : IRewardsProgramStatusesService
{
  public async Task<RewardsProgramStatusPage> FetchAsync(string? after = null, int limit = 25, CancellationToken cancellationToken = default)
  {
    var response = await client.FetchRewardsProgramStatusesAsync(after, limit, cancellationToken).ConfigureAwait(false);
    return new(response.Results.Select(RewardsProgramStatus.FromWire).ToArray(), response.PageInfo ?? new(null, false, null));
  }

  public async Task<RewardsProgramStatus> CreateAsync(CreateRewardsProgramStatusBody body, CancellationToken cancellationToken = default) =>
      RewardsProgramStatus.FromWire((await client.CreateRewardsProgramStatusAsync(body, cancellationToken).ConfigureAwait(false)).RewardsProgramStatus);

  public async Task<RewardsProgramStatus> UpdateAsync(string id, UpdateRewardsProgramStatusBody body, CancellationToken cancellationToken = default) =>
      RewardsProgramStatus.FromWire((await client.UpdateRewardsProgramStatusAsync(id, body, cancellationToken).ConfigureAwait(false)).RewardsProgramStatus);

  public Task DeleteAsync(string id, CancellationToken cancellationToken = default) => client.DeleteRewardsProgramStatusAsync(id, cancellationToken);

  public async Task<IReadOnlyList<RewardsProgramStatusOption>> SearchAsync(string query, CancellationToken cancellationToken = default) =>
      (await client.SearchRewardsProgramStatusTopicsAsync(query, cancellationToken: cancellationToken).ConfigureAwait(false)).Results
      .Where(value => value.Id is not null && value.Name is not null && value.Slug is not null &&
          value.TopicType == "rewards_program_status")
      .GroupBy(value => value.Id!, StringComparer.Ordinal)
      .Select(group => group.First())
      .Select(value => new RewardsProgramStatusOption(value.Id!, value.Name!, value.Slug!, value.TopicType!)).ToArray();
}
