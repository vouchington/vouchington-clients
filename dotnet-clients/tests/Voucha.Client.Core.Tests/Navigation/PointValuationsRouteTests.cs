using System.Runtime.CompilerServices;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class PointValuationsRouteTests
{
  [Fact]
  public void HasExactAuthenticatedSettingsDestination()
  {
    var signedIn = NativeDeepLinkResolver.Resolve(
        "voucha://my/rewards-program-point-valuations", new NavigationViewer(true, []));
    var signedOut = NativeDeepLinkResolver.Resolve(
        "voucha://my/rewards-program-point-valuations", NavigationViewer.Anonymous);
    Assert.Equal(NativeRouteDestinationId.PointValuations, signedIn.DestinationId);
    Assert.Equal(NavigationCatalog.SettingsIntentId, signedIn.IntentId);
    Assert.True(signedIn.CanNavigate);
    Assert.True(signedOut.ShouldQueueUntilAuthenticated);
    Assert.Null(NativeRouteCatalog.Entries.Single(entry =>
        entry.DestinationId == NativeRouteDestinationId.ProfileSettings)
        .Match("/my/rewards-program-point-valuations"));
  }

  [Fact]
  public void DirectPageWiresBeforeGenericIntentWithoutUuidFallback()
  {
    var appLinks = Source("Voucha.Client.App", "AppShell.AppLinks.cs");
    var direct = Source("Voucha.Client.App", "AppShell.PointValuationsRoute.cs");
    var xaml = Source("Voucha.Client.App", "Pages", "PointValuationsPage.xaml");
    Assert.True(appLinks.IndexOf("TryOpenPointValuationsRouteAsync", StringComparison.Ordinal) <
        appLinks.IndexOf("OpenIntentAsync(intentId, match: resolution.Match)", StringComparison.Ordinal));
    Assert.Contains("GetRequiredService<PointValuationsPage>()", direct, StringComparison.Ordinal);
    Assert.Contains("SearchRows", xaml, StringComparison.Ordinal);
    Assert.Contains("HybridPaginationControl", xaml, StringComparison.Ordinal);
    Assert.Contains("DisplayAlertAsync", Source("Voucha.Client.App", "Pages", "PointValuationsPage.xaml.cs"), StringComparison.Ordinal);
    Assert.DoesNotContain("Value.Id", xaml, StringComparison.Ordinal);
  }

  private static string Source(params string[] parts) =>
      File.ReadAllText(Path.Combine(new[] { RepoRoot(), "dotnet-clients", "src" }.Concat(parts).ToArray()));

  private static string RepoRoot([CallerFilePath] string sourcePath = "")
  {
    var directory = new DirectoryInfo(Path.GetDirectoryName(sourcePath)!);
    while (directory is not null)
    {
      if (Directory.Exists(Path.Combine(directory.FullName, "dotnet-clients"))) return directory.FullName;
      directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("Repository root not found.");
  }
}
