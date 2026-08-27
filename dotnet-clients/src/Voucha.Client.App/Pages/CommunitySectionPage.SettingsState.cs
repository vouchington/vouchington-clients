using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public abstract partial class CommunitySectionPage
{
  private void UpdateSettingsSwitches()
  {
    if (viewModel.Community is not { } community)
    {
      return;
    }
    if (settingsAllowReviewPostsSwitch is { } review)
    {
      review.IsToggled = community.AllowReviewPosts;
    }
    if (settingsAllowDataPointPostsSwitch is { } dataPoint)
    {
      dataPoint.IsToggled = community.AllowDataPointPosts;
    }
  }

  private IReadOnlyList<CommunitySummaryRow> Rows() =>
      section switch
      {
        CommunityDetailSurfaceSection.Members => viewModel.Members.Select(member => Row(
            member.Id,
            UiText.UserContent(member.DisplayName),
            UiText.ProtocolValue(member.Role),
            Date(member.CreatedAt))).ToArray(),
        CommunityDetailSurfaceSection.Posts => viewModel.Posts.Select(post => Row(
            post.Id,
            UiText.UserContent(post.Title),
            UiText.ProtocolValue(post.PostType),
            Date(post.CreatedAt))).ToArray(),
        CommunityDetailSurfaceSection.News => viewModel.News,
        CommunityDetailSurfaceSection.Lists => viewModel.ListItemCounts is { } counts
            ? [
                CountRow("topics", UiMessageKey.NativeDotnetCsharpCommunitiesTopics, counts.Topic),
                CountRow("sources", UiMessageKey.NativeDotnetCsharpCommunitiesSources, counts.RssFeed),
                CountRow("posts", UiMessageKey.NativeDotnetCsharpCommunitiesPosts, counts.Post),
                CountRow("domains-urls", UiMessageKey.NativeSwiftRebasedRouteSurfacesDomainsAndUrls, counts.UrlHostname + counts.Url),
            ]
            : [],
        CommunityDetailSurfaceSection.PinnedPosts => viewModel.PinnedPosts,
        CommunityDetailSurfaceSection.Applications => viewModel.Applications,
        CommunityDetailSurfaceSection.Invites => viewModel.Invites,
        CommunityDetailSurfaceSection.Settings => viewModel.Moderation,
        CommunityDetailSurfaceSection.Moderation or CommunityDetailSurfaceSection.Bans or CommunityDetailSurfaceSection.Restrictions or
            CommunityDetailSurfaceSection.ModeratorVacation or CommunityDetailSurfaceSection.AiAgents or CommunityDetailSurfaceSection.AgentPrompts or
            CommunityDetailSurfaceSection.Modlog or CommunityDetailSurfaceSection.Modmail or CommunityDetailSurfaceSection.ModerationAnalytics => viewModel.Moderation,
        _ => [
          CountRow("members", UiMessageKey.NativeDotnetCsharpCommunitiesMembers, viewModel.Members.Count),
          CountRow("posts", UiMessageKey.NativeDotnetCsharpCommunitiesPosts, viewModel.Posts.Count),
        ],
      };

  private static CommunitySummaryRow CountRow(string id, UiMessageKey key, int count) =>
      Row(
          id,
          UiText.Localized(key),
          UiText.Verbatim(UiCopy.FormatNumber(count)));

  private static CommunitySummaryRow Row(
      string id,
      UiText title,
      UiText subtitle,
      UiText? detail = null) =>
      new(id, title, subtitle, detail, UiCopy.CurrentLocalization);

  private static UiText? Date(DateTimeOffset? value) =>
      value is { } date ? UiText.Verbatim(UiCopy.FormatDateTime(date)) : null;

  private static View RowTemplate()
  {
    var title = new Label { FontAttributes = FontAttributes.Bold };
    title.SetBinding(Label.TextProperty, nameof(CommunitySummaryRow.Title));
    var subtitle = new Label();
    subtitle.SetBinding(Label.TextProperty, nameof(CommunitySummaryRow.Subtitle));
    var detail = new Label();
    detail.SetBinding(Label.TextProperty, nameof(CommunitySummaryRow.Detail));
    return new VerticalStackLayout { Spacing = 3, Children = { title, subtitle, detail } };
  }
}
