using Voucha.Client.Core.Api;
using Voucha.Client.Core.Bookmarks;

namespace Voucha.Client.Core.NewsFeeds;

public sealed partial class NewsFeedsViewModel
{
  private static NewsFeedItem[] ToggleArticleBookmark(
      IReadOnlyList<NewsFeedItem> sourceItems,
      string itemId,
      BookmarkPredicate predicate,
      bool active,
      bool removeOnActivate)
  {
    if (predicate == BookmarkPredicate.Hide && active && removeOnActivate)
    {
      return sourceItems.Where(item => !string.Equals(item.Id, itemId, StringComparison.Ordinal)).ToArray();
    }

    return sourceItems
        .Select(item => string.Equals(item.Id, itemId, StringComparison.Ordinal)
            ? item with
            {
              IsSaved = predicate == BookmarkPredicate.Save ? active : item.IsSaved,
              IsHidden = predicate == BookmarkPredicate.Hide ? active : item.IsHidden,
            }
            : item)
        .ToArray();
  }

}
