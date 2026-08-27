using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Xunit;

namespace Voucha.Client.Core.Tests.Friends;

public sealed class FriendRecommendationsPagePaginationSourceTests
{
  [Fact]
  public void PaginationControlRemainsOutsideEmptyCollectionView()
  {
    var document = XDocument.Load(PagePath());
    var pagination = Assert.Single(
        document.Descendants(),
        element => element.Name.LocalName == "HybridPaginationControl");

    Assert.DoesNotContain(
        pagination.Ancestors(),
        ancestor => ancestor.Name.LocalName is "CollectionView" or "CollectionView.Footer");
    Assert.Equal("1", pagination.Attributes().Single(attribute =>
        attribute.Name.LocalName == "Grid.Row").Value);
  }

  [Fact]
  public void TemporaryUnloadDetachesAndReloadReattachesPaginationWithoutDisposal()
  {
    var source = File.ReadAllText(PagePath("FriendRecommendationsPage.xaml.cs"));

    Assert.Contains("Loaded += OnLoaded", source, StringComparison.Ordinal);
    Assert.Contains("Unloaded += OnUnloaded", source, StringComparison.Ordinal);
    Assert.Contains("AttachPaginationHandler()", source, StringComparison.Ordinal);
    Assert.Contains("paginationHandlerAttached", source, StringComparison.Ordinal);
    Assert.Contains("PaginationControl.LoadNextPageRequested -= OnLoadNextPageRequested", source, StringComparison.Ordinal);
    Assert.Contains("protected override void OnParentSet()", source, StringComparison.Ordinal);
    Assert.Contains("else if (hadNavigationParent)", source, StringComparison.Ordinal);
    var unloadLifecycle = source[
        source.IndexOf("private void OnUnloaded", StringComparison.Ordinal)..source.IndexOf("protected override void OnParentSet", StringComparison.Ordinal)];
    Assert.DoesNotContain("viewModel.Dispose()", unloadLifecycle, StringComparison.Ordinal);
    Assert.Contains("viewModel.Dispose()", source, StringComparison.Ordinal);
  }

  private static string PagePath(
      string fileName = "FriendRecommendationsPage.xaml",
      [CallerFilePath] string sourcePath = "")
  {
    var directory = new DirectoryInfo(Path.GetDirectoryName(sourcePath)!);
    while (directory is not null)
    {
      var path = Path.Combine(
          directory.FullName,
          "dotnet-clients",
          "src",
          "Voucha.Client.App",
          "Pages",
          fileName);
      if (File.Exists(path)) return path;
      directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("Could not find FriendRecommendationsPage.xaml.");
  }
}
