using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Notifications;

/// <summary>Copy for post and RSS item notifications.</summary>
internal static class NotificationPostText
{
  private static readonly IReadOnlyDictionary<string, (UiMessageKey Anonymous, UiMessageKey ByUser)> NewTitles =
      new Dictionary<string, (UiMessageKey, UiMessageKey)>(StringComparer.Ordinal)
      {
        ["review"] = (UiMessageKey.NotificationsKindPostNewReviewAnonymous, UiMessageKey.NotificationsKindPostNewReviewByUser),
        ["discussion"] = (UiMessageKey.NotificationsKindPostNewDiscussionAnonymous, UiMessageKey.NotificationsKindPostNewDiscussionByUser),
        ["data_point"] = (UiMessageKey.NotificationsKindPostNewDataPointAnonymous, UiMessageKey.NotificationsKindPostNewDataPointByUser),
        ["link"] = (UiMessageKey.NotificationsKindPostNewLinkAnonymous, UiMessageKey.NotificationsKindPostNewLinkByUser),
        ["other"] = (UiMessageKey.NotificationsKindPostNewOtherAnonymous, UiMessageKey.NotificationsKindPostNewOtherByUser),
      };

  private static readonly IReadOnlyDictionary<string, (UiMessageKey Anonymous, UiMessageKey ByUser)> ManualSendTitles =
      new Dictionary<string, (UiMessageKey, UiMessageKey)>(StringComparer.Ordinal)
      {
        ["review"] = (UiMessageKey.NotificationsKindPostManualSendReviewAnonymous, UiMessageKey.NotificationsKindPostManualSendReviewByUser),
        ["discussion"] = (UiMessageKey.NotificationsKindPostManualSendDiscussionAnonymous, UiMessageKey.NotificationsKindPostManualSendDiscussionByUser),
        ["data_point"] = (UiMessageKey.NotificationsKindPostManualSendDataPointAnonymous, UiMessageKey.NotificationsKindPostManualSendDataPointByUser),
        ["link"] = (UiMessageKey.NotificationsKindPostManualSendLinkAnonymous, UiMessageKey.NotificationsKindPostManualSendLinkByUser),
        ["other"] = (UiMessageKey.NotificationsKindPostManualSendOtherAnonymous, UiMessageKey.NotificationsKindPostManualSendOtherByUser),
      };

  public static NotificationText PostNew(NotificationRenderInput input)
  {
    var post = input.Post(input.Notification.PostId);
    var username = input.Username(post?.ActorUserId);
    return new(ByUser(NewTitles, post?.PostType, username), Snippet(post?.Snippet));
  }

  public static NotificationText PostManualSend(NotificationRenderInput input)
  {
    var post = input.Post(input.Notification.PostId);
    var username = input.Username(input.Notification.SentById);
    return new(ByUser(ManualSendTitles, post?.PostType, username), Snippet(post?.Snippet));
  }

  public static NotificationText PostReply(NotificationRenderInput input)
  {
    var post = input.Post(input.Notification.PostId);
    return new(
        Named(
            input.Username(post?.ActorUserId),
            UiMessageKey.NotificationsKindPostReplyByUser,
            UiMessageKey.NotificationsKindPostReplyAnonymous),
        Snippet(post?.Snippet));
  }

  public static NotificationText PostRemoved(NotificationRenderInput input) =>
      NotificationText.TitleOnly(UiText.Localized(UiMessageKey.NotificationsKindPostRemovedTitle));

  public static NotificationText RssItemNew(NotificationRenderInput input)
  {
    var item = input.FeedItem();
    UiText feedName = item?.FeedName is { } name
        ? UiText.UserContent(name)
        : UiText.Localized(UiMessageKey.NotificationsKindRssItemNewFallbackFeed);
    return new(
        UiText.Localized(UiMessageKey.NotificationsKindRssItemNewTitle, ("feedName", feedName)),
        Snippet(item?.Snippet));
  }

  public static NotificationText RssItemManualSend(NotificationRenderInput input) =>
      new(
          Named(
              input.Username(input.Notification.SentById),
              UiMessageKey.NotificationsKindRssItemManualSendByUser,
              UiMessageKey.NotificationsKindRssItemManualSendAnonymous),
          Snippet(input.FeedItem()?.Snippet));

  /// <summary>The key with <c>{username}</c> when the user is known, otherwise the anonymous key.</summary>
  internal static UiText Named(string? username, UiMessageKey byUser, UiMessageKey anonymous) =>
      username is null
          ? UiText.Localized(anonymous)
          : UiText.Localized(byUser, ("username", UiText.UserContent(username)));

  private static UiText ByUser(
      IReadOnlyDictionary<string, (UiMessageKey Anonymous, UiMessageKey ByUser)> titles,
      string? postType,
      string? username)
  {
    var keys = titles[postType is not null && titles.ContainsKey(postType) ? postType : "other"];
    return Named(username, keys.ByUser, keys.Anonymous);
  }

  private static IReadOnlyList<UiText> Snippet(string? snippet) =>
      string.IsNullOrEmpty(snippet) ? [] : [UiText.UserContent(snippet)];
}
