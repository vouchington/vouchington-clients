using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

internal sealed class BookmarkCollectionPaginationFooter : VerticalStackLayout
{
  public BookmarkCollectionPaginationFooter(BookmarkCollectionViewModel viewModel)
  {
    ArgumentNullException.ThrowIfNull(viewModel);
    AutomationId = "bookmark-collection-pagination";
    Padding = new Thickness(0, 12);
    Spacing = 8;
    BindingContext = viewModel;

    var error = new Label
    {
      AutomationId = "bookmark-collection-continuation-error",
      TextColor = Colors.IndianRed,
      HorizontalTextAlignment = TextAlignment.Center,
    };
    error.SetBinding(Label.TextProperty, nameof(BookmarkCollectionViewModel.ContinuationErrorMessage));
    error.SetBinding(IsVisibleProperty, nameof(BookmarkCollectionViewModel.HasContinuationError));

    var loadMore = new Button { AutomationId = "bookmark-collection-load-more" };
    loadMore.SetDynamicResource(Button.TextProperty, UiMessageKey.NativeSwiftCommonLoadMore.Value);
    loadMore.SetBinding(IsVisibleProperty, nameof(BookmarkCollectionViewModel.ShowLoadMore));
    loadMore.SetBinding(IsEnabledProperty, nameof(BookmarkCollectionViewModel.CanLoadMorePosts));
    loadMore.Clicked += async (_, _) => await viewModel.LoadMoreAsync().ConfigureAwait(true);

    var retry = new Button { AutomationId = "bookmark-collection-retry" };
    retry.SetDynamicResource(Button.TextProperty, UiMessageKey.NativeSwiftCommonTryAgain.Value);
    retry.SetBinding(IsVisibleProperty, nameof(BookmarkCollectionViewModel.HasContinuationError));
    retry.SetBinding(IsEnabledProperty, nameof(BookmarkCollectionViewModel.CanRetryContinuation));
    retry.Clicked += async (_, _) => await viewModel.LoadMoreAsync().ConfigureAwait(true);

    Children.Add(error);
    Children.Add(loadMore);
    Children.Add(retry);
  }
}
