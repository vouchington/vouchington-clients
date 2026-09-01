using System.Runtime.CompilerServices;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class EmbedPreviewSurfaceSourceTests
{
  [Theory]
  [InlineData("NewsFeedsPage.xaml")]
  [InlineData("PostsPage.xaml")]
  public void ListSurfacesRenderTheSafeEmbedPreview(string page)
  {
    var source = Source(page);

    Assert.Contains("EmbedPreview.Provider", source, StringComparison.Ordinal);
    Assert.Contains("EmbedPreview.ThumbnailUrl", source, StringComparison.Ordinal);
    Assert.Contains("EmbedPreview.CanPlay", source, StringComparison.Ordinal);
  }

  [Theory]
  [InlineData("BookmarkCollectionPage.EmbedPreview.cs")]
  [InlineData("CommunityDetailPage.EmbedPreview.cs")]
  [InlineData("CommunitySectionPage.EmbedPreview.cs")]
  [InlineData("PostDetailPage.EmbedPreview.cs")]
  public void NativeCollectionSurfacesOfferDedicatedPlayerNavigation(string page)
  {
    var source = Source(page);

    Assert.Contains("EmbedPlayerPage", source, StringComparison.Ordinal);
    Assert.Contains("SourceUrl", source, StringComparison.Ordinal);
  }

  [Fact]
  public void PostDetailRendersEmbedPreviewForRootAncestorsAndDescendants()
  {
    var page = Source("PostDetailPage.xaml");
    var ancestors = page.Split("<VerticalStackLayout Spacing=\"8\" IsVisible=\"{Binding HasAncestors}\">", 2)[1]
        .Split("<HorizontalStackLayout Spacing=\"8\">", 2)[0];

    Assert.Equal(3, page.Split("EmbedPreview.HasPreview").Length - 1);
    Assert.Contains("EmbedPreview.CanPlay", ancestors, StringComparison.Ordinal);
    Assert.Contains("EmbedPreview.HasSource", ancestors, StringComparison.Ordinal);
    Assert.Contains("OnEmbedPlayClicked", ancestors, StringComparison.Ordinal);
    Assert.Contains("OnEmbedOpenClicked", ancestors, StringComparison.Ordinal);
  }

  private static string Source(string file, [CallerFilePath] string sourceFile = "")
  {
    var root = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!);
    while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "src"))) root = root.Parent;
    return File.ReadAllText(Path.Combine(root!.FullName, "src", "Voucha.Client.App", "Pages", file));
  }
}
