namespace Voucha.Client.Core.Localization;

public static partial class UiTaxonomy
{
  public static UiText ListVisibility(string? value) =>
      LocalizedOrProtocol(value switch
      {
        "private" => UiMessageKey.NativeTaxonomyListsPrivate,
        "unlisted" => UiMessageKey.NativeTaxonomyListsUnlisted,
        "public" => UiMessageKey.NativeTaxonomyListsPublic,
        _ => null,
      }, value);

  public static UiText ListItemType(string? value) =>
      LocalizedOrProtocol(value switch
      {
        "rss_feed_item" => UiMessageKey.NativeTaxonomyListsRssFeedItem,
        "post" => UiMessageKey.NativeTaxonomyListsPost,
        _ => null,
      }, value);

  public static UiText MediaType(string? value) =>
      LocalizedOrProtocol(value switch
      {
        "article" => UiMessageKey.NativeTaxonomyListsArticle,
        "audio" => UiMessageKey.NativeTaxonomyListsAudio,
        "video" => UiMessageKey.NativeTaxonomyListsVideo,
        "item" or null => UiMessageKey.NativeTaxonomyListsItem,
        _ => null,
      }, value);

  public static UiText NewsFeedSourceType(string? value) =>
      LocalizedOrProtocol(value switch
      {
        "article" => UiMessageKey.NativeDotnetNewsFeedsNewsSource,
        "podcast" => UiMessageKey.NativeDotnetNewsFeedsPodcastSource,
        "video" => UiMessageKey.NativeDotnetNewsFeedsVideoSource,
        null => UiMessageKey.NativeDotnetNewsFeedsSource,
        _ => null,
      }, value);

  public static UiText TopicType(string? value) =>
      LocalizedOrProtocol(value switch
      {
        "topic" => UiMessageKey.NativeTaxonomyTopicsTopic,
        "rss_feed" => UiMessageKey.NativeTaxonomyTopicsRssFeed,
        _ => null,
      }, value);

  public static UiText PostType(string? value) =>
      LocalizedOrProtocol(value switch
      {
        "article" => UiMessageKey.NativeDotnetPostsPostTypeArticle,
        "blog_post" => UiMessageKey.NativeDotnetPostsPostTypeBlog,
        "data_point" => UiMessageKey.NativeDotnetPostsPostTypeDataPoint,
        "discussion" => UiMessageKey.NativeDotnetPostsPostTypeDiscussion,
        "link" => UiMessageKey.NativeDotnetPostsPostTypeLink,
        "review" => UiMessageKey.NativeDotnetPostsPostTypeReview,
        "story" => UiMessageKey.NativeTaxonomyPostsStory,
        "comment" => UiMessageKey.NativeTaxonomyPostsComment,
        "topic_recommendation" => UiMessageKey.NativeTaxonomyPostsTopicRecommendation,
        "post" or null => UiMessageKey.NativeDotnetPostsPostTypePost,
        _ => null,
      }, value);

  public static UiText CommunityMemberRole(string? value) =>
      LocalizedOrProtocol(value switch
      {
        "owner" => UiMessageKey.NativeSwiftPresentationValuesOwner,
        "moderator" => UiMessageKey.NativeSwiftPresentationValuesModerator,
        "member" => UiMessageKey.NativeSwiftPresentationValuesMember,
        _ => null,
      }, value);

  public static UiText DataRequestStatus(string? value) =>
      LocalizedOrProtocol(value switch
      {
        "pending" => UiMessageKey.NativeTaxonomyModerationPending,
        "processing" => UiMessageKey.NativeTaxonomyDataRequestProcessing,
        "ready" => UiMessageKey.NativeTaxonomyDataRequestReady,
        "failed" => UiMessageKey.NativeTaxonomyDataRequestFailed,
        "expired" => UiMessageKey.NativeTaxonomyDataRequestExpired,
        _ => null,
      }, value);

  public static UiText LandingPageItemType(string? value) =>
      LocalizedOrProtocol(value switch
      {
        "profile_link" => UiMessageKey.NativeDotnetResidualProfileLink,
        "review" => UiMessageKey.NativeDotnetResidualReview,
        "referral_link" => UiMessageKey.NativeDotnetResidualReferralLink,
        "topic_group" => UiMessageKey.NativeDotnetResidualTopicGroup,
        "link" => UiMessageKey.NativeDotnetResidualLink,
        _ => null,
      }, value);

  private static UiText LocalizedOrProtocol(UiMessageKey? key, string? value) =>
      key is { } messageKey
          ? UiText.Localized(messageKey)
          : UiText.ProtocolValue(value);
}
