using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Growth;

public sealed class ApiGrowthMetricsService(VouchaApiClient client) : IGrowthMetricsService
{
  public Task<GrowthMetricsResponse> FetchGrowthMetricsAsync(
      GrowthMetricsRange range,
      CancellationToken cancellationToken = default) =>
      client.FetchGrowthMetricsAsync(range, cancellationToken);
}
