using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Bookmarks;

internal static partial class BookmarkCollectionRowFactory
{
  public static string DestinationForPost(Post post) =>
      $"/{PostSegment(post.PostType)}/{Uri.EscapeDataString(
          string.Equals(post.PostType, "topic_recommendation", StringComparison.Ordinal)
              ? post.Id
              : post.Slug ?? post.Id)}";

  private static string PostSegment(string? postType) => postType switch
  {
    "discussion" => "discussion",
    "story" => "story",
    "article" => "article",
    "blog" or "blog_post" => "blog-post",
    "link" => "link",
    "data_point" => "data-point",
    "topic_recommendation" => "topic-recommendations",
    _ => "review",
  };

  private static string TopicDestination(Topic topic)
  {
    var identity = Uri.EscapeDataString(
        string.Equals(topic.TopicType, "topic_recommendation", StringComparison.Ordinal)
            ? topic.Id
            : topic.Slug ?? topic.Id);
    var segment = topic.TopicType switch
    {
      "rss_feed" or "source" => "source",
      "topic_recommendation" => "topic-recommendations",
      "fediverse_instance" => "instance",
      "bank_account" => "bank-account",
      "referral_program" => "referral-program",
      "rewards_program" => "rewards-program",
      "rewards_program_status" => "rewards-program-status",
      "card" => "card",
      _ => "topic",
    };
    return string.Concat("/", segment, "/", identity);
  }
}
