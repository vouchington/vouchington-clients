namespace Voucha.Client.Core.FeatureFlags;

public static class FeatureFlagOverridePolicy
{
  public static bool CanManage(IReadOnlyList<string>? roles) =>
      roles?.Any(role => role is "administrator" or "developer") == true;
}
