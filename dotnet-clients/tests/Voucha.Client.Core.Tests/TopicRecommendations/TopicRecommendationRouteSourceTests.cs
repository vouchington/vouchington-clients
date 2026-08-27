using Xunit;

namespace Voucha.Client.Core.Tests.TopicRecommendations;

public sealed class TopicRecommendationRouteSourceTests
{
  [Fact]
  public void AppShellDispatchesRecommendationDetailToReadOnlyNativePage()
  {
    var source = File.ReadAllText(RepoPath(
        "dotnet-clients", "src", "Voucha.Client.App", "AppShell.TopicRoutes.cs"));
    var postRoutes = File.ReadAllText(RepoPath(
        "dotnet-clients", "src", "Voucha.Client.App", "AppShell.PostRoutes.cs"));

    Assert.Contains("TopicRecommendationRoute.TryGetDetailId", source, StringComparison.Ordinal);
    Assert.Contains("new TopicRecommendationDetailPage", source, StringComparison.Ordinal);
    Assert.DoesNotContain("TopicRecommendationRoute", postRoutes, StringComparison.Ordinal);
    Assert.DoesNotContain("TopicRecommendations", postRoutes, StringComparison.Ordinal);
  }

  private static string RepoPath(params string[] parts)
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
      var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
      if (File.Exists(candidate)) return candidate;
      directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("Could not find repository root from test output directory.");
  }
}
