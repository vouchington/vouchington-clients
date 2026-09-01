using System.Runtime.CompilerServices;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class ProviderEmbedPlayerSourceTests
{
  [Fact]
  public void PlayerUsesTheNarrowPlatformWebViewBoundary()
  {
    Assert.Contains("ProviderEmbedWebView", Source("Pages", "EmbedPlayerPage.cs"), StringComparison.Ordinal);
    var page = Source("Pages", "EmbedPlayerPage.cs");
    Assert.Contains("Uri sourceUrl", page, StringComparison.Ordinal);
    Assert.Contains("IsValidSource(sourceUrl)", page, StringComparison.Ordinal);
    Assert.DoesNotContain("open.IsVisible", page, StringComparison.Ordinal);
    var shared = Source("Controls", "ProviderEmbedWebView.cs");
    Assert.Contains("HeightRequest = 200", shared, StringComparison.Ordinal);
    Assert.Contains("https://{AppInfo.Current.PackageName}/", shared, StringComparison.Ordinal);
    Assert.Contains("DisconnectHandler", shared, StringComparison.Ordinal);
    Assert.Contains("IsApprovedPlayer", shared, StringComparison.Ordinal);
    var mac = Source("Controls", "ProviderEmbedWebViewHandler.MacCatalyst.cs");
    Assert.Contains("NonPersistentDataStore", mac, StringComparison.Ordinal);
    Assert.Contains("new WKWebView", mac, StringComparison.Ordinal);
    var windows = Source("Controls", "ProviderEmbedWebView.Windows.cs");
    Assert.Contains("IsWebMessageEnabled = false", windows, StringComparison.Ordinal);
    Assert.Contains("NewWindowRequested", windows, StringComparison.Ordinal);
    Assert.Contains("CreateWebResourceRequest", windows, StringComparison.Ordinal);
    var handler = Source("Controls", "ProviderEmbedWebViewHandler.Windows.cs");
    Assert.Contains("Guid.NewGuid", handler, StringComparison.Ordinal);
    Assert.Contains("Directory.Delete", handler, StringComparison.Ordinal);
    Assert.Contains("Interlocked.Exchange", handler, StringComparison.Ordinal);
    Assert.Contains("Cancellation.Cancel", handler, StringComparison.Ordinal);
    Assert.Contains("await active.Initialization", handler, StringComparison.Ordinal);
    Assert.Contains("SweepOwnedProfiles", handler, StringComparison.Ordinal);
    Assert.Contains("PendingCleanup", handler, StringComparison.Ordinal);
    Assert.Contains("CleanupInProgress", handler, StringComparison.Ordinal);
    Assert.Contains("ActiveProfiles", handler, StringComparison.Ordinal);
    Assert.Contains("!ActiveProfiles.ContainsKey(folder)", handler, StringComparison.Ordinal);
    Assert.Contains("ActiveProfiles.TryRemove(folder", handler, StringComparison.Ordinal);
    Assert.Contains("RetryCleanupAsync", handler, StringComparison.Ordinal);
    Assert.Contains("MaxCleanupAttempts", handler, StringComparison.Ordinal);
    Assert.Contains("PendingCleanup.Keys", handler, StringComparison.Ordinal);
    Assert.Contains("for (var attempt", handler, StringComparison.Ordinal);
    Assert.DoesNotContain("while (true)", handler, StringComparison.Ordinal);
    Assert.Contains("Guid.TryParseExact", handler, StringComparison.Ordinal);
    Assert.Contains("Path.GetFullPath(ProfileRoot)", handler, StringComparison.Ordinal);
  }

  [Fact]
  public void RssDetailRendersPreviewAndKeepsSourceVisible()
  {
    var page = Source("Pages", "RssFeedItemDetailPage.cs") + Source("Pages", "RssFeedItemDetailPage.EmbedPreview.cs");
    Assert.Contains("EmbedPreview.Provider", page, StringComparison.Ordinal);
    Assert.Contains("EmbedPreview.Title", page, StringComparison.Ordinal);
    Assert.Contains("EmbedPreview.Description", page, StringComparison.Ordinal);
    Assert.Contains("EmbedPreview.ThumbnailUrl", page, StringComparison.Ordinal);
    Assert.Contains("EmbedPreview.HasSource", page, StringComparison.Ordinal);
    Assert.Contains("OnEmbedOpenClicked", page, StringComparison.Ordinal);
  }

  private static string Source(string directory, string file, [CallerFilePath] string sourceFile = "")
  {
    var root = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!);
    while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "src"))) root = root.Parent;
    return File.ReadAllText(Path.Combine(root!.FullName, "src", "Voucha.Client.App", directory, file));
  }
}
