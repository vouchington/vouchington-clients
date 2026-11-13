using Voucha.Client.Core.Api;
using Voucha.Client.Core.Posts;

namespace Voucha.Client.App.Pages;

public partial class PostsPage
{
  private FetchPostsRequest? browseScope;

  // The view model clears the filter when the backend flag turns off; mirror it so the page never
  // re-sends (or toggles against) a selection the view model already dropped.
  private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
  {
    if (e.PropertyName == nameof(PostsListViewModel.HackerNewsFilter)) hasHackerNewsReferences = viewModel.HackerNewsFilter;
  }

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

  /// <summary>Switches this already-open page back to the personal feed and clears browse filters.</summary>
  public Task ApplyFeedModeAsync()
  {
    browseScope = null;
    mode = "feed";
    postTypes = null;
    hasHackerNewsReferences = null;
    FeedButton.IsVisible = AllButton.IsVisible = ReviewsButton.IsVisible = DiscussionsButton.IsVisible = true;
    return LoadCurrentAsync();
  }

  private async void OnTimeRangeSelected(object? sender, string value) =>
      await viewModel.SelectBrowseTimeRangeAsync(value).ConfigureAwait(true);
}
