using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class AiCostsRouteTests
{
  [Theory]
  [InlineData(false, null, false, true)]
  [InlineData(true, "member", false, false)]
  [InlineData(true, "administrator", true, false)]
  public void AiCostsDeepLinkIsAdministratorOnly(bool authenticated, string? role, bool canNavigate, bool queuesSignIn)
  {
    var roles = role is null ? Array.Empty<string>() : new[] { role };
    var resolution = NativeDeepLinkResolver.Resolve(
        "voucha://admin/ai-costs",
        new NavigationViewer(authenticated, roles));

    Assert.Equal(NativeRouteDestinationId.EngineeringAiCosts, resolution.DestinationId);
    Assert.Equal(canNavigate, resolution.CanNavigate);
    Assert.Equal(queuesSignIn, resolution.ShouldQueueUntilAuthenticated);
  }

  [Fact]
  public void AppShellAndAdministratorHubRouteToDedicatedAiCostsPage()
  {
    var routing = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.App", "AppShell.Engineering.cs"));
    var hubCode = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.App", "Pages", "EngineeringPage.xaml.cs"));
    var hubXaml = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.App", "Pages", "EngineeringPage.xaml"));
    var services = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.App", "MauiProgram.Engineering.cs"));

    Assert.Contains("path == \"/admin/ai-costs\"", routing, StringComparison.Ordinal);
    Assert.Contains("GetRequiredService<AiCostsPage>()", routing, StringComparison.Ordinal);
    Assert.Contains("OperationsCards.IsVisible = roles?.Contains(\"administrator\"", hubCode, StringComparison.Ordinal);
    Assert.Contains("OnAiCostsClicked", hubCode, StringComparison.Ordinal);
    Assert.Contains("x:Name=\"OperationsCards\"", hubXaml, StringComparison.Ordinal);
    Assert.Contains("AutomationId=\"engineering-open-ai-costs\"", hubXaml, StringComparison.Ordinal);
    Assert.Contains("AddTransient<AiCostsPage>()", services, StringComparison.Ordinal);
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
