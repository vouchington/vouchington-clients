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

  /// <summary>Applies a later post route to this already-open page and reloads it.</summary>
  public Task ApplyBrowseScopeAsync(FetchPostsRequest scope)
  {
    ConfigureBrowseScope(scope);
    return LoadCurrentAsync();
  }

  private async void OnTimeRangeSelected(object? sender, string value) =>
      await viewModel.SelectBrowseTimeRangeAsync(value).ConfigureAwait(true);
}
