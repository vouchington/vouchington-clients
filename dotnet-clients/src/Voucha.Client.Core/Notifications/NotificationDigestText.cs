using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Notifications;

/// <summary>Copy for the community activity digest, including its derived net and rate figures.</summary>
internal static class NotificationDigestText
{
  public static NotificationText Render(NotificationRenderInput input)
  {
    var communityCount = input.NumberParam("community_count");
    var joins = input.NumberParam("joins");
    var departures = input.NumberParam("departures");
    var total = input.NumberParam("total_members");
    var active = input.NumberParam("active_posters");
    var values = new List<(string Name, object? Value)>
    {
      ("communities", UiText.Localized(
          communityCount == 1
              ? UiMessageKey.NotificationsKindCommunityActivityDigestCommunitiesOne
              : UiMessageKey.NotificationsKindCommunityActivityDigestCommunitiesOther,
          ("count", UiText.Number(communityCount)))),
      ("joins", UiText.Number(joins)),
      ("departures", UiText.Number(departures)),
      ("net", UiText.Number(joins - departures)),
      ("total", UiText.Number(total)),
      ("discussions", UiText.Number(input.NumberParam("discussions"))),
      ("reviews", UiText.Number(input.NumberParam("reviews"))),
      ("dataPoints", UiText.Number(input.NumberParam("data_points"))),
      ("comments", UiText.Number(input.NumberParam("comments"))),
      ("active", UiText.Number(active)),
      ("rate", UiText.Number(total == 0 ? 0 : Math.Round(active * 100 / total, MidpointRounding.AwayFromZero))),
      ("moderation", UiText.Number(input.NumberParam("moderation_workload"))),
    };
    var title = UiText.Localized(UiMessageKey.NotificationsKindCommunityActivityDigestTitle);
    var top = input.Post(input.Notification.TopPostId);
    if (top is null || input.Community(top.CommunityId) is not { } topCommunity)
    {
      return NotificationText.Of(
          title,
          UiText.Localized(UiMessageKey.NotificationsKindCommunityActivityDigestBody, [.. values]));
    }

    values.Add(("top", UiText.Localized(
        UiMessageKey.NotificationsKindCommunityActivityDigestTop,
        ("community", UiText.UserContent(topCommunity.Name)),
        ("title", UiText.UserContent(top.Snippet)),
        ("replies", UiText.Number(input.NumberParam("top_discussion_replies"))))));
    return NotificationText.Of(
        title,
        UiText.Localized(UiMessageKey.NotificationsKindCommunityActivityDigestBodyWithTop, [.. values]));
  }
}
