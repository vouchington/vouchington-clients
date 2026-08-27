using System.Runtime.CompilerServices;
using Xunit;

namespace Voucha.Client.Core.Tests.Bookmarks;

public sealed class BookmarkCollectionPageSourceTests
{
  [Fact]
  public void PageWiresLocalizedLoadMoreAndRetryControlsToContinuationState()
  {
    var page = ReadPage("BookmarkCollectionPage.cs");
    var pagination = ReadPage("BookmarkCollectionPaginationFooter.cs");

    Assert.Contains("rows.Footer = new BookmarkCollectionPaginationFooter(viewModel);", page, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeSwiftCommonLoadMore", pagination, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeSwiftCommonTryAgain", pagination, StringComparison.Ordinal);
    Assert.Contains("nameof(BookmarkCollectionViewModel.ShowLoadMore)", pagination, StringComparison.Ordinal);
    Assert.Contains("nameof(BookmarkCollectionViewModel.CanLoadMorePosts)", pagination, StringComparison.Ordinal);
    Assert.Contains("nameof(BookmarkCollectionViewModel.HasContinuationError)", pagination, StringComparison.Ordinal);
    Assert.Contains("nameof(BookmarkCollectionViewModel.CanRetryContinuation)", pagination, StringComparison.Ordinal);
    Assert.Equal(2, pagination.Split("await viewModel.LoadMoreAsync()", StringSplitOptions.None).Length - 1);
    Assert.DoesNotContain("WebView", pagination, StringComparison.Ordinal);
  }

  private static string ReadPage(string file, [CallerFilePath] string sourceFile = "") =>
      File.ReadAllText(RepoPath(
          sourceFile, "dotnet-clients", "src", "Voucha.Client.App", "Pages", file));

  private static string RepoPath(string sourceFile, params string[] segments)
  {
    foreach (var start in new[] { Path.GetDirectoryName(sourceFile)!, Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
      var directory = new DirectoryInfo(start);
      while (directory is not null)
      {
        var candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
        if (File.Exists(candidate)) return candidate;
        directory = directory.Parent;
      }
    }
    throw new DirectoryNotFoundException("Could not find repository root from test output directory.");
  }
}
