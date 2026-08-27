using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.RewardsProgramStatuses;

public interface IRewardsProgramStatusesService
{
  Task<RewardsProgramStatusPage> FetchAsync(string? after = null, int limit = 25, CancellationToken cancellationToken = default);
  Task<RewardsProgramStatus> CreateAsync(CreateRewardsProgramStatusBody body, CancellationToken cancellationToken = default);
  Task<RewardsProgramStatus> UpdateAsync(string id, UpdateRewardsProgramStatusBody body, CancellationToken cancellationToken = default);
  Task DeleteAsync(string id, CancellationToken cancellationToken = default);
  Task<IReadOnlyList<RewardsProgramStatusOption>> SearchAsync(string query, CancellationToken cancellationToken = default);
}
