using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.PointValuations;

public interface IPointValuationsService
{
  Task<PointValuationPage> FetchAsync(string? after = null, int limit = 25, CancellationToken cancellationToken = default);
  Task<PointValuation> CreateAsync(CreatePointValuationBody body, CancellationToken cancellationToken = default);
  Task<PointValuation> UpdateAsync(string id, UpdatePointValuationBody body, CancellationToken cancellationToken = default);
  Task DeleteAsync(string id, CancellationToken cancellationToken = default);
  Task<IReadOnlyList<RewardsProgramOption>> SearchAsync(string query, CancellationToken cancellationToken = default);
}

public sealed record PointValuationPage(IReadOnlyList<PointValuation> Results, PageInfo PageInfo);
