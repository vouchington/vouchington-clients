using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Growth;

public interface IGrowthMetricsService
{
  Task<GrowthMetricsResponse> FetchGrowthMetricsAsync(
      GrowthMetricsRange range,
      CancellationToken cancellationToken = default);
}
