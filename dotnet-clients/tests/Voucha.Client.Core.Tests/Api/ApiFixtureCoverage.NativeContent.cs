using System.Text.Json;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

internal static partial class ApiFixtureCoverage
{
  public static Dictionary<string, Type> WithNativeContentFixtures(this Dictionary<string, Type> registry)
  {
    registry["native.comments.post-detail.default"] = typeof(PostResponse);
    registry["native.posts.hacker-news.detail"] = typeof(PostResponse);
    foreach (var id in new[]
    {
      "default", "short-continuation", "empty-continuation", "after-filtered-head", "expand-preview-child",
    })
    {
      registry[$"native.comments.descendants.{id}"] = typeof(PostThreadResponse);
    }

    foreach (var id in new[] { "permalink", "bounded.shallow", "bounded.deep-initial", "bounded.deep-continuation" })
    {
      registry[$"native.comments.ancestors.{id}"] = typeof(PostThreadResponse);
    }

    registry["native.rss-feed-items.hacker-news.detail"] = typeof(RssFeedItemResponse);
    registry["native.rss-feed-items.hacker-news.list"] = typeof(RssFeedItemsFeedResponse);
    registry["native.podcast-episodes.hacker-news.list"] = typeof(RssFeedItemsFeedResponse);
    registry["native.posts.time-range.all"] = typeof(PostsFeedResponse);
    registry["native.posts.time-range.explicit-week"] = typeof(PostsFeedResponse);
    foreach (var id in new[] { "one-day", "seven-day.1m", "seven-day.1y", "seven-day.all", "union.first-page", "union.next-page" })
    {
      registry[$"native.rss-personal.{id}"] = typeof(RssFeedItemsFeedResponse);
    }

    registry["native.rss-feeds.create.accepted"] = typeof(RssFeedImportSubmission);
    foreach (var state in new[] { "pending", "completed", "failed" })
    {
      registry[$"native.rss-feeds.create.status.{state}"] = typeof(RssFeedImportStatus);
    }

    registry["native.rss-feeds.delete.accepted"] = typeof(JsonElement);
    registry["native.rss-feeds.delete.retained-import-conflict"] = typeof(JsonElement);
    registry["native.topics.merge.accepted"] = typeof(TopicMergeResponse);
    foreach (var state in new[] { "queued", "running", "completed", "failed" })
    {
      registry[$"native.topics.merge.status.{state}"] = typeof(TopicMergeResponse);
    }

    return registry;
  }

  private static Dictionary<string, ApiRequest> WithNativeContentEndpoints(this Dictionary<string, ApiRequest> registry) =>
      NativeContentFixtureRequests.AddTo(registry);
}
