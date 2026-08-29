using System.Runtime.CompilerServices;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class PostComposePageCategorySourceTests
{
  [Fact]
  public void DiscussionCategoriesBindBothAddControlsAndRemovableRows()
  {
    var xaml = PageSource("PostComposePage.xaml");
    var codeBehind = PageSource("PostComposePage.Categories.cs");

    Assert.Contains("DiscussionCategoryHashtag", xaml, StringComparison.Ordinal);
    Assert.Contains("DiscussionCategories", xaml, StringComparison.Ordinal);
    Assert.Contains("OnAddDiscussionCategoryTopicClicked", xaml, StringComparison.Ordinal);
    Assert.Contains("OnAddDiscussionCategoryHashtagClicked", xaml, StringComparison.Ordinal);
    Assert.Contains("OnRemoveDiscussionCategoryClicked", xaml, StringComparison.Ordinal);
    Assert.Contains("AddDiscussionCategoryTopic", codeBehind, StringComparison.Ordinal);
    Assert.Contains("AddDiscussionCategoryHashtag", codeBehind, StringComparison.Ordinal);
    Assert.Contains("RemoveDiscussionCategory", codeBehind, StringComparison.Ordinal);
  }

  private static string PageSource(string file, [CallerFilePath] string sourceFile = "")
  {
    var root = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!);
    while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "src"))) root = root.Parent;
    return File.ReadAllText(Path.Combine(
        root?.FullName ?? throw new DirectoryNotFoundException(),
        "src", "Voucha.Client.App", "Pages", file));
  }
}
