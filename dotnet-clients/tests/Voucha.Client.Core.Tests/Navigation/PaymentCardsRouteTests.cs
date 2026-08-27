using Voucha.Client.Core.Navigation;
using System.Runtime.CompilerServices;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class PaymentCardsRouteTests
{
  [Fact]
  public void CardsHaveAnExactAuthenticatedDestination()
  {
    var signedIn = NativeDeepLinkResolver.Resolve("voucha://my/cards", new NavigationViewer(true, []));
    var signedOut = NativeDeepLinkResolver.Resolve("voucha://my/cards", NavigationViewer.Anonymous);
    Assert.Equal(NativeRouteDestinationId.PaymentCards, signedIn.DestinationId);
    Assert.Equal(NavigationCatalog.SettingsIntentId, signedIn.IntentId);
    Assert.True(signedIn.CanNavigate);
    Assert.Equal(NativeRouteDestinationId.PaymentCards, signedOut.DestinationId);
    Assert.True(signedOut.ShouldQueueUntilAuthenticated);
    Assert.Null(NativeRouteCatalog.Entries
        .Single(entry => entry.DestinationId == NativeRouteDestinationId.ProfileSettings).Match("/my/cards"));
  }

  [Fact]
  public void CardsPageWiresNativeCrudControlsWithoutUuidPresentation()
  {
    var appLinks = Source("Voucha.Client.App", "AppShell.AppLinks.cs");
    var direct = Source("Voucha.Client.App", "AppShell.PaymentCardsRoute.cs");
    var xaml = Source("Voucha.Client.App", "Pages", "PaymentCardsPage.xaml");
    var code = Source("Voucha.Client.App", "Pages", "PaymentCardsPage.xaml.cs");
    Assert.True(appLinks.IndexOf("TryOpenPaymentCardsRouteAsync", StringComparison.Ordinal) <
        appLinks.IndexOf("OpenIntentAsync(intentId, match: resolution.Match)", StringComparison.Ordinal));
    Assert.Contains("GetRequiredService<PaymentCardsPage>()", direct, StringComparison.Ordinal);
    Assert.Contains("TopicRows", xaml, StringComparison.Ordinal);
    Assert.Contains("Text=\"{Binding TopicSearchQuery, Mode=TwoWay}\"", xaml, StringComparison.Ordinal);
    Assert.Contains("ParentCandidates", xaml, StringComparison.Ordinal);
    Assert.Contains("DatePicker", xaml, StringComparison.Ordinal);
    Assert.Contains("Keyboard=\"Numeric\"", xaml, StringComparison.Ordinal);
    Assert.Contains("DisplayAlertAsync", code, StringComparison.Ordinal);
    Assert.DoesNotContain("Value.Id", xaml, StringComparison.Ordinal);
  }

  private static string Source(params string[] parts)
  {
    var path = Path.Combine(new[] { RepoRoot(), "dotnet-clients", "src" }.Concat(parts).ToArray());
    return File.ReadAllText(path);
  }

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
