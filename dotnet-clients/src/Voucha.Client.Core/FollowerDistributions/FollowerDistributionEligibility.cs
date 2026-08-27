namespace Voucha.Client.Core.FollowerDistributions;

public static class FollowerDistributionEligibility
{
  public static bool IsPublicTopLevelPost(
      string? postType,
      string? parentId,
      string? privacy,
      string? broadcast) =>
      !string.Equals(postType, "comment", StringComparison.Ordinal) &&
      string.IsNullOrWhiteSpace(parentId) &&
      string.Equals(privacy, "public", StringComparison.Ordinal) &&
      (string.Equals(broadcast, "everyone", StringComparison.Ordinal) ||
       string.Equals(broadcast, "users", StringComparison.Ordinal));
}
