using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class ProviderEmbedPlayerSourceTests
{
  [Fact]
  public void PlayerUsesTheNarrowPlatformWebViewBoundary()
  {
    Assert.Contains("ProviderEmbedWebView", AppSource.Read("Pages", "EmbedPlayerPage.cs"), StringComparison.Ordinal);
    var page = AppSource.Read("Pages", "EmbedPlayerPage.cs");
    Assert.Contains("Uri sourceUrl", page, StringComparison.Ordinal);
    Assert.Contains("IsValidSource(sourceUrl)", page, StringComparison.Ordinal);
    Assert.DoesNotContain("open.IsVisible", page, StringComparison.Ordinal);
    Assert.Contains("active.Unload()", page, StringComparison.Ordinal);
    Assert.Contains("player = new ProviderEmbedWebView()", page, StringComparison.Ordinal);
    Assert.Contains("playerLayout.Remove(active)", page, StringComparison.Ordinal);
    var shared = AppSource.Read("Controls", "ProviderEmbedWebView.cs");
    Assert.Contains("HeightRequest = 200", shared, StringComparison.Ordinal);
    Assert.Contains("https://{AppInfo.Current.PackageName}/", shared, StringComparison.Ordinal);
    Assert.Contains("DisconnectHandler", shared, StringComparison.Ordinal);
    Assert.Contains("IsApprovedPlayer", shared, StringComparison.Ordinal);
    var mac = AppSource.Read("Controls", "ProviderEmbedWebViewHandler.MacCatalyst.cs");
    Assert.Contains("NonPersistentDataStore", mac, StringComparison.Ordinal);
    Assert.Contains("new WKWebView", mac, StringComparison.Ordinal);
    var windows = AppSource.Read("Controls", "ProviderEmbedWebView.Windows.cs");
    Assert.Contains("IsWebMessageEnabled = false", windows, StringComparison.Ordinal);
    Assert.Contains("NewWindowRequested", windows, StringComparison.Ordinal);
    Assert.Contains("CreateWebResourceRequest", windows, StringComparison.Ordinal);
    var handler = AppSource.Read("Controls", "ProviderEmbedWebViewHandler.Windows.cs");
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
    var page = AppSource.Read("Pages", "RssFeedItemDetailPage.cs") + AppSource.Read("Pages", "RssFeedItemDetailPage.EmbedPreview.cs");
    Assert.Contains("EmbedPreview.Provider", page, StringComparison.Ordinal);
    Assert.Contains("EmbedPreview.Title", page, StringComparison.Ordinal);
    Assert.Contains("EmbedPreview.Description", page, StringComparison.Ordinal);
    Assert.Contains("EmbedPreview.ThumbnailUrl", page, StringComparison.Ordinal);
    Assert.Contains("EmbedPreview.HasSource", page, StringComparison.Ordinal);
    Assert.Contains("OnEmbedOpenClicked", page, StringComparison.Ordinal);
  }

  [Fact]
  public void FeedSurfacesChooseTheEmbedSourceActionWhenAvailable()
  {
    var news = AppSource.Read("Pages", "NewsFeedsPage.xaml");
    var rss = AppSource.Read("Pages", "RssFeedItemDetailPage.cs");

    Assert.Contains("CanOpenExternally", news, StringComparison.Ordinal);
    Assert.Contains("EmbedPreview.HasSource", news, StringComparison.Ordinal);
    Assert.Contains("CanOpenExternally", rss, StringComparison.Ordinal);
    Assert.Contains("EmbedPreview.HasSource", rss, StringComparison.Ordinal);
  }

  [Fact]
  public void ProfileHistoryRendersAndOperatesEmbedPreview()
  {
    var page = AppSource.Read("Pages", "ProfilePage.xaml") + AppSource.Read("Pages", "ProfilePage.EmbedPreview.cs");

    Assert.Contains("EmbedPreview.Provider", page, StringComparison.Ordinal);
    Assert.Contains("IsVisible=\"{Binding ShowEmbed}\"", page, StringComparison.Ordinal);
    Assert.Contains("CanPlayEmbed", page, StringComparison.Ordinal);
    Assert.Contains("HasEmbedSource", page, StringComparison.Ordinal);
    Assert.Contains("EmbedPlayerPage", page, StringComparison.Ordinal);
  }
}
