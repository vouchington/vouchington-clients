using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Tests.Support;
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
    var routing = File.ReadAllText(RepositoryFilePath.Find("dotnet-clients", "src", "Voucha.Client.App", "AppShell.Engineering.cs"));
    var hubCode = File.ReadAllText(RepositoryFilePath.Find("dotnet-clients", "src", "Voucha.Client.App", "Pages", "EngineeringPage.xaml.cs"));
    var hubXaml = File.ReadAllText(RepositoryFilePath.Find("dotnet-clients", "src", "Voucha.Client.App", "Pages", "EngineeringPage.xaml"));
    var services = File.ReadAllText(RepositoryFilePath.Find("dotnet-clients", "src", "Voucha.Client.App", "MauiProgram.Engineering.cs"));

    Assert.Contains("path == \"/admin/ai-costs\"", routing, StringComparison.Ordinal);
    Assert.Contains("GetRequiredService<AiCostsPage>()", routing, StringComparison.Ordinal);
    Assert.Contains("OperationsCards.IsVisible = roles?.Contains(\"administrator\"", hubCode, StringComparison.Ordinal);
    Assert.Contains("OnAiCostsClicked", hubCode, StringComparison.Ordinal);
    Assert.Contains("x:Name=\"OperationsCards\"", hubXaml, StringComparison.Ordinal);
    Assert.Contains("AutomationId=\"engineering-open-ai-costs\"", hubXaml, StringComparison.Ordinal);
    Assert.Contains("AddTransient<AiCostsPage>()", services, StringComparison.Ordinal);
  }
}
