namespace Voucha.Client.Core.Navigation;

public static class NativeEntityDetailPaths
{
  public static string Topic(string id, string? slug, string? topicType)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(id);
    var type = string.IsNullOrWhiteSpace(topicType) ? "topic" : topicType;
    var identifier = string.Equals(type, "topic_recommendation", StringComparison.Ordinal)
        ? id
        : slug ?? id;
    var segment = type switch
    {
      "rss_feed" or "source" => "source",
      "fediverse_instance" => "instance",
      "topic_recommendation" => "topic-recommendations",
      _ => type.Replace('_', '-'),
    };
    return NativeRoutePath.Entity(segment, identifier);
  }

  public static string Source(string feedId, string? topicId, string? topicSlug)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(feedId);
    return NativeRoutePath.Entity("source", topicSlug ?? topicId ?? feedId);
  }
}
