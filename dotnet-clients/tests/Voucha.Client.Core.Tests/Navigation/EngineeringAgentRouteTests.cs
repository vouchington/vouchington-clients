using Voucha.Client.Core.Api;
using Voucha.Client.Core.Engineering;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class EngineeringAgentRouteTests
{
  [Fact]
  public void AgentDirectoryAndDetailRoutesUseDistinctPages()
  {
    var routing = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.App", "AppShell.Engineering.cs"));
    var agentRouting = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.App", "AppShell.EngineeringAgents.cs"));
    var services = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.App", "MauiProgram.Engineering.cs"));
    var detailRoute = routing[routing.IndexOf("if (match?.Param(\"idOrSlug\") is { } listAgent", StringComparison.Ordinal)..];

    Assert.Contains("GetRequiredService<AgentListsPage>()", routing, StringComparison.Ordinal);
    Assert.Contains("GetRequiredService<AgentDetailPage>()", detailRoute, StringComparison.Ordinal);
    Assert.DoesNotContain("GetRequiredService<AgentListsPage>()", detailRoute, StringComparison.Ordinal);
    Assert.Contains("page is AgentDetailPage", routing, StringComparison.Ordinal);
    Assert.Contains("AddTransient<AgentDetailPage>()", services, StringComparison.Ordinal);
    Assert.Contains("TryCreateInitialEngineeringAgentRoot(match, out var agentRoot)", routing, StringComparison.Ordinal);
    Assert.Contains("AgentNavigationTargets.Conversation(agent, conversation)", agentRouting, StringComparison.Ordinal);
    Assert.Contains("agentListsPage.SetInitialTarget(", agentRouting, StringComparison.Ordinal);
    Assert.Contains("AgentNavigationTargets.Detail(agent, AgentConversationFilter.FromQuery(match.QueryItems))", agentRouting, StringComparison.Ordinal);
    Assert.Contains("TryPrepareEngineeringAgentRouteAsync(page, match, targetPage)", routing, StringComparison.Ordinal);
    Assert.Contains("shellContentPage.Navigation", agentRouting, StringComparison.Ordinal);
    Assert.Contains("NavigationStack.OfType<AgentListsPage>()", agentRouting, StringComparison.Ordinal);
    Assert.Contains("await navigation.PopToRootAsync(animated: false)", agentRouting, StringComparison.Ordinal);
    Assert.Contains("if (!navigation.NavigationStack.Contains(page)) return;", agentRouting, StringComparison.Ordinal);
    Assert.Contains("for (var remainingPages = navigation.NavigationStack.Count;", agentRouting, StringComparison.Ordinal);
    Assert.Contains("await navigation.PopAsync", agentRouting, StringComparison.Ordinal);
    Assert.Contains("!currentDetail.MatchesContext(agent)", agentRouting, StringComparison.Ordinal);
    Assert.Contains("PushAsync(parentDetailPage, animated: false)", agentRouting, StringComparison.Ordinal);
    Assert.True(
        routing.IndexOf("TryCreateInitialEngineeringAgentRoot(match, out var agentRoot)", StringComparison.Ordinal) <
        routing.IndexOf("TryCreateEngineeringRoutePage(match, intent, out var page)", StringComparison.Ordinal));
    Assert.True(
        routing.IndexOf("TryPrepareEngineeringAgentRouteAsync(page, match, targetPage)", StringComparison.Ordinal) <
        routing.IndexOf("if (page.GetType() == targetPage.GetType())", StringComparison.Ordinal));
  }

  [Fact]
  public void AgentDetailDeepLinkPreservesTheHighestPrecedenceSupportedFilter()
  {
    var match = Assert.IsType<NativeRouteMatch>(NativeRouteCatalog
        .MatchingRoute("/agent/helper?username=alice&post_slug=slug")?.Match);
    var target = AgentNavigationTargets.Detail(
        Assert.IsType<string>(match.Param("idOrSlug")),
        AgentConversationFilter.FromQuery(match.QueryItems));

    Assert.Equal("/agent/helper?username=alice", target.Value);
  }

  private static string RepoPath(params string[] segments)
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
      var candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
      if (File.Exists(candidate)) return candidate;
      directory = directory.Parent;
    }

    throw new DirectoryNotFoundException("Could not find repository root from test output directory.");
  }
}
