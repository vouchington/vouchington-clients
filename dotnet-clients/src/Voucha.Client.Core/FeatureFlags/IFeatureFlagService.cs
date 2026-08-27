using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.FeatureFlags;

public interface IFeatureFlagService
{
  Task<FeatureFlagsResponse> FetchAsync(CancellationToken cancellationToken = default);
}

public sealed class ApiFeatureFlagService(VouchaApiClient client) : IFeatureFlagService
{
  public Task<FeatureFlagsResponse> FetchAsync(CancellationToken cancellationToken = default) =>
      client.FetchFeatureFlagsAsync(cancellationToken);
}
