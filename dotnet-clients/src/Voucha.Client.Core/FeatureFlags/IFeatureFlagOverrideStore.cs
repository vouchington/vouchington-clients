namespace Voucha.Client.Core.FeatureFlags;

public interface IFeatureFlagOverrideStore
{
  Task<IReadOnlyDictionary<string, bool>> LoadAsync(CancellationToken cancellationToken = default);
  Task SaveAsync(IReadOnlyDictionary<string, bool> values, CancellationToken cancellationToken = default);
}
