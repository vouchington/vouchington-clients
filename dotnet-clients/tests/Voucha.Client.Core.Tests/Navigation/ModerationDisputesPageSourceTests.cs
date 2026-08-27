using System.Runtime.CompilerServices;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class ModerationDisputesPageSourceTests
{
  [Fact]
  public void StaffDisputesUseDedicatedPageWhileMemberDisputesRemainGeneric()
  {
    var routing = RepoFile(
        "dotnet-clients",
        "src",
        "Voucha.Client.App",
        "AppShell.ModerationRoutes.cs");
    var wiring = RepoFile(
        "dotnet-clients",
        "src",
        "Voucha.Client.App",
        "MauiProgram.ModerationDisputes.cs");

    Assert.Contains(
        "ModerationRouteKind.Disputes && !context.Mine",
        routing,
        StringComparison.Ordinal);
    Assert.Contains("ModerationDisputesPage", routing, StringComparison.Ordinal);
    Assert.Contains("ModerationDisputesPageFactory", routing, StringComparison.Ordinal);
    Assert.Contains("ModerationDisputesPageLifetime.Replace", routing, StringComparison.Ordinal);
    Assert.Contains(
        "AddSingleton<ModerationDisputesPageFactory>()",
        wiring,
        StringComparison.Ordinal);
    Assert.DoesNotContain("AddTransient<ModerationDisputesPage>", wiring, StringComparison.Ordinal);
    Assert.DoesNotContain(
        "AddTransient(sp => new ModerationDisputesViewModel",
        wiring,
        StringComparison.Ordinal);
    Assert.DoesNotContain(
        "ModerationRouteKind.Disputes && context.Mine",
        routing,
        StringComparison.Ordinal);
  }

  [Fact]
  public void DedicatedPageCarriesRoleDenialAndLifecycleControls()
  {
    var page = RepoFile(
        "dotnet-clients",
        "src",
        "Voucha.Client.App",
        "Pages",
        "ModerationDisputesPage.cs");
    var card = RepoFile(
        "dotnet-clients",
        "src",
        "Voucha.Client.App",
        "Pages",
        "ModerationDisputeCardView.cs");

    Assert.Contains("review-disputes-role-denied", page, StringComparison.Ordinal);
    Assert.Contains("SetAnnotationDraft", card, StringComparison.Ordinal);
    Assert.Contains("SetPublicResponseDraft", card, StringComparison.Ordinal);
    Assert.Contains("ModerationDisputeResolutionAction.Remove", card, StringComparison.Ordinal);
    Assert.Contains("ModerationDisputeResolutionAction.Annotate", card, StringComparison.Ordinal);
    Assert.Contains("ModerationDisputeResolutionAction.Dismiss", card, StringComparison.Ordinal);
    Assert.Contains("RunLatestLoadAsync(viewModel.LoadMoreAsync)", page, StringComparison.Ordinal);
    Assert.Contains("IDisposable", page, StringComparison.Ordinal);
  }

  private static string RepoFile(params string[] segments)
  {
    var directory = new DirectoryInfo(SourceDirectory());
    while (directory is not null)
    {
      var candidate = Path.Combine([directory.FullName, .. segments]);
      if (File.Exists(candidate)) return File.ReadAllText(candidate);
      directory = directory.Parent;
    }
    throw new DirectoryNotFoundException();
  }

  private static string SourceDirectory([CallerFilePath] string sourcePath = "") =>
      Path.GetDirectoryName(sourcePath) ?? throw new DirectoryNotFoundException();
}
