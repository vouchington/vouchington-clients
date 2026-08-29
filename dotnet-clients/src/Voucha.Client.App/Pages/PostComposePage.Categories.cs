using Voucha.Client.Core.Posts;

namespace Voucha.Client.App.Pages;

public partial class PostComposePage
{
  private void OnAddDiscussionCategoryTopicClicked(object? sender, EventArgs e) =>
      viewModel.AddDiscussionCategoryTopic();

  private void OnAddDiscussionCategoryHashtagClicked(object? sender, EventArgs e) =>
      viewModel.AddDiscussionCategoryHashtag();

  private void OnRemoveDiscussionCategoryClicked(object? sender, EventArgs e)
  {
    if (sender is Button { BindingContext: PostComposeCategoryDraft category })
    {
      viewModel.RemoveDiscussionCategory(category);
    }
  }
}
