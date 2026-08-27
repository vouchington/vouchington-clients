using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Bookmarks;

public enum BookmarkCollectionKind
{
  Posts,
  Topics,
  Users,
  RssFeedItems,
  RssFeeds,
  Urls,
  Hostnames,
  Communities,
}

public sealed record BookmarkCollectionRouteContext(
    string Path,
    UiText TitleText,
    BookmarkCollectionKind Kind,
    string ListType,
    string? MediaType = null,
    string? FeedType = null)
{
  public BookmarkInverseAction? InverseAction => BookmarkCollectionActions.For(Kind, ListType);

  public BookmarkCollectionRouteContext(
      string path,
      UiMessageKey titleKey,
      BookmarkCollectionKind kind,
      string listType,
      string? MediaType = null,
      string? FeedType = null)
      : this(path, UiText.Localized(titleKey), kind, listType, MediaType, FeedType)
  {
  }

  public string Title => UiLocalization.English.Resolve(TitleText);
}
