using Voucha.Client.Core.NewsFeeds;

namespace Voucha.Client.App.Pages;

public partial class NewsFeedsPage
{
  private async void OnLoadMoreStoryArticlesClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: NewsFeedItem item }) await viewModel.LoadMoreStoryArticlesAsync(item);
  }
}
