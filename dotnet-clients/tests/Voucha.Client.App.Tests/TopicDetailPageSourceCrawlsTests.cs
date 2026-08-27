using System.Runtime.CompilerServices;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class TopicDetailPageSourceCrawlsTests
{
  [Fact]
  public void SourceCrawlRoutesRenderTheMemberVisibleCrawlHistoryBinding()
  {
    var pageSource = AppSource("Pages", "TopicDetailPage.xaml.cs");
    var markup = AppSource("Pages", "TopicDetailPage.xaml");
    var routeSource = AppSource("", "AppShell.TopicRoutes.cs");
    var topicsPage = AppSource("Pages", "TopicsPage.xaml.cs");

    Assert.Contains("if (showSourceCrawls) await viewModel.LoadSourceCrawlsAsync()", pageSource, StringComparison.Ordinal);
    Assert.Contains("ItemsSource=\"{Binding SourceCrawls}\"", markup, StringComparison.Ordinal);
    Assert.Contains("IsVisible=\"{Binding HasLoadedSourceCrawlHistory}\"", markup, StringComparison.Ordinal);
    Assert.Contains("<CollectionView.EmptyView>", markup, StringComparison.Ordinal);
    Assert.Contains("native.dotnet.residual.crawlHistoryEmpty", markup, StringComparison.Ordinal);
    Assert.Contains("{app:UiLocalizedValue Path=CreatedAt, Format=dateTime}", markup, StringComparison.Ordinal);
    Assert.Contains("{app:UiLocalizedValue Path=SelectedSourceCrawl.CreatedAt, Format=dateTime}", markup, StringComparison.Ordinal);
    Assert.Contains("native.dotnet.residual.crawlHistory", markup, StringComparison.Ordinal);
    Assert.Contains("OnShowSourceCrawlsClicked", markup, StringComparison.Ordinal);
    Assert.Contains("OnLoadMoreSourceCrawlsClicked", markup, StringComparison.Ordinal);
    Assert.Contains("HybridPaginationControl", markup, StringComparison.Ordinal);
    Assert.Contains("HasSourceCrawlsContinuationError", markup, StringComparison.Ordinal);
    Assert.Contains("CanViewSourceCrawlHistory", markup, StringComparison.Ordinal);
    Assert.Contains("HasSourceCrawlHistoryAccessError", markup, StringComparison.Ordinal);
    Assert.Contains("OnSourceCrawlTapped", markup, StringComparison.Ordinal);
    Assert.Contains("SelectedSourceCrawl", markup, StringComparison.Ordinal);
    Assert.Contains("LoadMoreSourceCrawlsAsync", pageSource, StringComparison.Ordinal);
    Assert.Contains("LoadSourceCrawlAsync(sourceCrawlId)", pageSource, StringComparison.Ordinal);
    Assert.Contains("CreateSourceCrawlDetailViewModel()", pageSource, StringComparison.Ordinal);
    Assert.Contains("OnRetrySourceCrawlHistoryClicked", pageSource, StringComparison.Ordinal);
    Assert.Contains("HasSourceCrawlHistoryRequestError", markup, StringComparison.Ordinal);
    Assert.Contains("SourceCrawlHistoryRequestErrorMessage", markup, StringComparison.Ordinal);
    Assert.Contains("resolution.Match?.Template is \"/source/:idOrSlug/crawls\"", routeSource, StringComparison.Ordinal);
    Assert.Contains("canManageSourceCrawls", routeSource, StringComparison.Ordinal);
    Assert.Contains("requiresAuthoritativeSourceCrawlMembership: true", routeSource, StringComparison.Ordinal);
    Assert.Contains("apiClient: apiClient", topicsPage, StringComparison.Ordinal);
    Assert.Contains("canManageSourceCrawls", topicsPage, StringComparison.Ordinal);
    Assert.Contains("requiresAuthoritativeSourceCrawlMembership: true", topicsPage, StringComparison.Ordinal);
  }

  private static string AppSource(string directory, string file, [CallerFilePath] string sourceFile = "")
  {
    var root = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!);
    while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "src"))) root = root.Parent;
    return File.ReadAllText(Path.Combine(
        root?.FullName ?? throw new DirectoryNotFoundException(),
        "src", "Voucha.Client.App", directory, file));
  }
}
