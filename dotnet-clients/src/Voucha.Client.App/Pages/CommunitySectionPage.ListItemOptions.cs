using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public abstract partial class CommunitySectionPage
{
  private static readonly CommunityListItemOption[] ListItemTypes =
  [
    new("topic", "topics", UiMessageKey.NativeDotnetCsharpCommunitiesTopics),
    new("rss_feed", "rss-feeds", UiMessageKey.NativeDotnetCsharpCommunitiesSources),
    new("post", "posts", UiMessageKey.NativeDotnetCsharpCommunitiesPosts),
    new("url_hostname", "domains", UiMessageKey.NativeDotnetResidualDomains),
    new("url", "urls", UiMessageKey.NativeDotnetResidualUrls),
  ];

  private Picker ListItemTypePicker() =>
      LocalizedPicker(ListItemTypes.Select(option => option.LabelKey).ToArray());

  private static CommunityListItemOption SelectedListItemType(Picker picker) =>
      picker.SelectedIndex >= 0 && picker.SelectedIndex < ListItemTypes.Length
          ? ListItemTypes[picker.SelectedIndex]
          : ListItemTypes[0];

  private static CommunityListItemRequest ListItemRequest(Picker picker, string? id)
  {
    var type = SelectedListItemType(picker).ProtocolValue;
    return new(
        TopicId: type == "topic" ? id : null,
        RssFeedId: type == "rss_feed" ? id : null,
        PostId: type == "post" ? id : null,
        UrlHostnameId: type == "url_hostname" ? id : null,
        UrlId: type == "url" ? id : null);
  }

  private sealed record CommunityListItemOption(
      string ProtocolValue,
      string RouteSegment,
      UiMessageKey LabelKey);
}
