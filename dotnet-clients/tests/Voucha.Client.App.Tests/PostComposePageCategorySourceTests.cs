using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class PostComposePageCategorySourceTests
{
  [Fact]
  public void DiscussionCategoriesBindBothAddControlsAndRemovableRows()
  {
    var xaml = AppSource.Read("Pages", "PostComposePage.xaml");
    var codeBehind = AppSource.Read("Pages", "PostComposePage.Categories.cs");

    Assert.Contains("DiscussionCategoryHashtag", xaml, StringComparison.Ordinal);
    Assert.Contains("DiscussionCategories", xaml, StringComparison.Ordinal);
    Assert.Contains("OnAddDiscussionCategoryTopicClicked", xaml, StringComparison.Ordinal);
    Assert.Contains("OnAddDiscussionCategoryHashtagClicked", xaml, StringComparison.Ordinal);
    Assert.Contains("OnRemoveDiscussionCategoryClicked", xaml, StringComparison.Ordinal);
    Assert.Contains("AddDiscussionCategoryTopic", codeBehind, StringComparison.Ordinal);
    Assert.Contains("AddDiscussionCategoryHashtag", codeBehind, StringComparison.Ordinal);
    Assert.Contains("RemoveDiscussionCategory", codeBehind, StringComparison.Ordinal);
  }
}
