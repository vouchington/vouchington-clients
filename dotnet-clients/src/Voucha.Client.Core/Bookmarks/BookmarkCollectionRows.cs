namespace Voucha.Client.Core.Bookmarks;

using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

public sealed record BookmarkInverseAction(
    string EntityType,
    BookmarkPredicate Predicate,
    UiMessageKey LabelKey);

public sealed record BookmarkCollectionRow(
    string Id,
    string EntityType,
    UiText TitleText,
    IUiLocalization Localization,
    UiText? SubtitleText = null,
    UiText? DetailText = null,
    string? DestinationPath = null,
    BookmarkInverseAction? InverseAction = null,
    int Rank = 0,
    string? RootPostId = null,
    bool IsActionPending = false)
{
  public string Title => Localization.Resolve(TitleText);

  public string? Subtitle =>
      SubtitleText is { } text ? Localization.Resolve(text) : null;

  public string? Detail =>
      DetailText is { } text ? Localization.Resolve(text) : null;

  public bool HasAction => InverseAction is not null;

  public bool CanInvokeAction => HasAction && !IsActionPending;

  public string? ActionLabel =>
      InverseAction is { } action ? Localization.Localize(action.LabelKey) : null;

  public string? ActionAccessibilityLabel =>
      InverseAction is null
          ? null
          : Localization.Format(
              UiMessageKey.NativeSwiftHouseholdsBookmarksInverseActionAccessibility,
              ("action", UiText.Localized(InverseAction.LabelKey)),
              ("title", TitleText));

  public string OpenDetailsAccessibilityLabel =>
      Localization.Format(
          UiMessageKey.NativeDotnetBookmarksOpenDetailsAccessibility,
          ("title", TitleText));
}

internal readonly record struct BookmarkOperationIdentity(int Generation, string EntityType, string EntityId);
