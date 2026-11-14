using Voucha.Client.Core.Tests.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.TopicRecommendations;

public sealed class TopicRecommendationRouteSourceTests
{
  [Fact]
  public void AppShellDispatchesRecommendationDetailToReadOnlyNativePage()
  {
    var source = File.ReadAllText(RepositoryFilePath.Find(
        "dotnet-clients", "src", "Voucha.Client.App", "AppShell.TopicRoutes.cs"));
    var postRoutes = File.ReadAllText(RepositoryFilePath.Find(
        "dotnet-clients", "src", "Voucha.Client.App", "AppShell.PostRoutes.cs"));

    Assert.Contains("TopicRecommendationRoute.TryGetDetailId", source, StringComparison.Ordinal);
    Assert.Contains("new TopicRecommendationDetailPage", source, StringComparison.Ordinal);
    Assert.DoesNotContain("TopicRecommendationRoute", postRoutes, StringComparison.Ordinal);
    Assert.DoesNotContain("TopicRecommendations", postRoutes, StringComparison.Ordinal);
  }
}
