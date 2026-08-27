using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Tags;

public sealed record TagRelationTab(
    UiText LabelText,
    string Value,
    string Predicate,
    string ObjectType,
    IUiLocalization Localization)
{
  public TagRelationTab(
      UiMessageKey labelKey,
      string value,
      string predicate,
      string objectType)
      : this(UiText.Localized(labelKey), value, predicate, objectType, UiLocalization.English)
  {
  }

  public string Label => Localization.Resolve(LabelText);

  public TagRelationTab WithLocalization(IUiLocalization localization) =>
      this with { Localization = localization };
}

internal static class TagRelationConfigs
{
  internal static readonly IReadOnlyList<TagRelationTab> PostTagTabs =
  [
    new(UiMessageKey.NativeDotnetTagManagementCategoryTopics, "topic", "category", "topic"),
    new(UiMessageKey.NativeDotnetTagManagementRelatedPosts, "post", "related", "post"),
    new(UiMessageKey.NativeDotnetTagManagementRelatedLinks, "url", "related", "url"),
  ];

  internal static readonly IReadOnlyList<TagRelationTab> TopicTagTabs =
  [
    new(UiMessageKey.NativeDotnetTagManagementRelatedTopics, "topic", "related", "topic"),
    new(UiMessageKey.NativeDotnetTagManagementCategories, "category", "category", "topic"),
    new(UiMessageKey.NativeDotnetTagManagementPublisherType, "publisher_type", "publisher_type", "topic"),
    new(UiMessageKey.NativeDotnetTagManagementFaqPosts, "post", "faq", "post"),
    new(UiMessageKey.NativeDotnetTagManagementLandingPage, "landing_page", "landing_page", "url"),
    new(UiMessageKey.NativeDotnetTagManagementTermsOfService, "terms_of_service", "terms_of_service", "url"),
  ];

  private static readonly HashSet<string> TopicTagSegmentSet = new(TopicTagTabs.Select(tab => tab.Value), StringComparer.Ordinal);
  private static readonly HashSet<string> SourceOnlyTopicTagSegmentSet = new(["publisher_type"], StringComparer.Ordinal);

  internal static bool IsTopicTagSegment(string value) => TopicTagSegmentSet.Contains(value);

  internal static IReadOnlyList<TagRelationTab> GetTopicTagTabsForTopicType(string? topicType) =>
      topicType == "rss_feed"
          ? TopicTagTabs
          : TopicTagTabs.Where(tab => !SourceOnlyTopicTagSegmentSet.Contains(tab.Value)).ToArray();

  internal static bool IsTopicTagSegmentForTopicType(string value, string? topicType) =>
      GetTopicTagTabsForTopicType(topicType).Any(tab => tab.Value == value);

  internal static readonly (string EntityType, IReadOnlyList<TagRelationTab> Tabs)[] SupportedEntityTypes =
  [
    ("post", PostTagTabs),
    ("topic", TopicTagTabs),
    ("rss_feed_item", [new(UiMessageKey.NativeDotnetTagManagementCategories, "topic", "category", "topic")]),
  ];
}
