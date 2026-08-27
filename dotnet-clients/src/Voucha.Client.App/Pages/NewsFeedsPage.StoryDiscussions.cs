using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Posts;

namespace Voucha.Client.App.Pages;

public partial class NewsFeedsPage
{
  private async void OnStartStoryDiscussionClicked(object? sender, EventArgs e)
  {
    if (!sessionStore.Current.IsAuthenticated) return;
    if (sender is not Button { CommandParameter: NewsFeedItem item })
    {
      return;
    }

    var result = await viewModel.StartStoryDiscussionAsync(item);
    if (await emailRecovery.PresentIfRequestedAsync(this, viewModel.EmailVerificationGate))
    {
      return;
    }
    if (result is null)
    {
      return;
    }

    await PushStoryDiscussionAsync(result.PostId);
  }

  private async void OnOpenStoryDiscussionClicked(object? sender, EventArgs e)
  {
    if (sender is not Button { CommandParameter: NewsFeedItem { StoryPostId: { } postId } })
    {
      return;
    }

    await PushStoryDiscussionAsync(postId);
  }

  private Task PushStoryDiscussionAsync(string postId) =>
      Navigation.PushAsync(new PostDetailPage(
          serviceProvider.GetRequiredService<ICommentThreadService>(),
          serviceProvider.GetRequiredService<IPostsService>(),
          sessionStore,
          serviceProvider,
          postId));
}
