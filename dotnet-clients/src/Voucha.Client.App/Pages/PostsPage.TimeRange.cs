using Voucha.Client.Core.Api;

namespace Voucha.Client.App.Pages;

public partial class PostsPage
{
  private FetchPostsRequest? browseScope;

  public void ConfigureBrowseScope(FetchPostsRequest scope, bool topicScope = false)
  {
    browseScope = scope;
    mode = "browse";
    postTypes = scope.PostTypes;
    TimeRangePicker.IsVisible = true;
    FeedButton.IsVisible = !topicScope;
    AllButton.IsVisible = !topicScope;
    ReviewsButton.IsVisible = !topicScope;
    DiscussionsButton.IsVisible = !topicScope;
  }

  private async void OnTimeRangeSelected(object? sender, string value) =>
      await viewModel.SelectBrowseTimeRangeAsync(value).ConfigureAwait(true);
}
