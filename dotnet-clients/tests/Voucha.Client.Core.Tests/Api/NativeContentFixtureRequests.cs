using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

/// <summary>Endpoint builders for the native content-browsing, RSS lifecycle and topic-merge fixtures.</summary>
internal static class NativeContentFixtureRequests
{
  private const string Thread = "70000000-0000-7000-8000-000000005430";
  private const string FeedId = "70000000-0000-7000-8000-000000003680";
  private const string ImportId = "70000000-0000-7000-8000-000000003681";
  private const string MergeSource = "70000000-0000-7000-8000-000000005170";
  private const string MergeDestination = "70000000-0000-7000-8000-000000005171";
  private const string MergeOperation = "70000000-0000-7000-8000-000000005172";

  public static Dictionary<string, ApiRequest> AddTo(Dictionary<string, ApiRequest> registry)
  {
    AddComments(registry);
    AddFeeds(registry);
    AddLifecycles(registry);
    return registry;
  }

  private static void AddComments(Dictionary<string, ApiRequest> registry)
  {
    registry["native.comments.post-detail.default"] = VouchaApiEndpoints.Post("comment-root");
    registry["native.posts.hacker-news.detail"] = VouchaApiEndpoints.Post("comment-root");
    registry["native.comments.descendants.default"] = VouchaApiEndpoints.PostDescendants(
        "70000000-0000-7000-8000-000000005400", limit: 3);
    registry["native.comments.descendants.short-continuation"] = VouchaApiEndpoints.PostDescendants(Thread, limit: 3);
    registry["native.comments.descendants.empty-continuation"] = VouchaApiEndpoints.PostDescendants(
        Thread, After("native.comments.descendants.empty-continuation"), 1);
    registry["native.comments.descendants.after-filtered-head"] = VouchaApiEndpoints.PostDescendants(
        Thread, After("native.comments.descendants.after-filtered-head"), 3);
    registry["native.comments.descendants.expand-preview-child"] = VouchaApiEndpoints.PostDescendants(
        "70000000-0000-7000-8000-000000005434", limit: 3);
    registry["native.comments.ancestors.permalink"] =
        VouchaApiEndpoints.PostAncestors("70000000-0000-7000-8000-000000005402");
    registry["native.comments.ancestors.bounded.shallow"] =
        VouchaApiEndpoints.PostAncestors("70000000-0000-7000-8000-000000005402", limit: 5);
    registry["native.comments.ancestors.bounded.deep-initial"] =
        VouchaApiEndpoints.PostAncestors("bounded-ancestor-comment-7", limit: 5);
    registry["native.comments.ancestors.bounded.deep-continuation"] = VouchaApiEndpoints.PostAncestors(
        "bounded-ancestor-comment-7", "fixture-ancestor-deep-initial-end", 5);
  }

  private static void AddFeeds(Dictionary<string, ApiRequest> registry)
  {
    registry["native.rss-feed-items.hacker-news.detail"] =
        VouchaApiEndpoints.RssFeedItem("00000000-0000-7000-8000-000000007886");
    registry["native.rss-feed-items.hacker-news.list"] = VouchaApiEndpoints.AllRssFeedItems();
    registry["native.podcast-episodes.hacker-news.list"] =
        VouchaApiEndpoints.RssFeedItems(mediaType: "audio");
    registry["native.posts.time-range.all"] = TimeRangePosts("all");
    registry["native.posts.time-range.explicit-week"] = TimeRangePosts("1w");
    registry["native.users.profile.posts.staff-provenance"] = VouchaApiEndpoints.AllPosts(
        new FetchPostsRequest(PostTypes: "discussion,review", Creator: "user-abc", Sort: "new"));
    registry["native.rss-personal.one-day"] = Personal("1d");
    registry["native.rss-personal.seven-day.1m"] = Personal("1m");
    registry["native.rss-personal.seven-day.1y"] = Personal("1y");
    registry["native.rss-personal.seven-day.all"] = Personal("all");
    registry["native.rss-personal.union.first-page"] = Personal("all", limit: 2);
    registry["native.rss-personal.union.next-page"] =
        Personal("all", After("native.rss-personal.union.next-page"), 2);
  }

  private static void AddLifecycles(Dictionary<string, ApiRequest> registry)
  {
    registry["native.rss-feeds.create.accepted"] =
        VouchaApiEndpoints.CreateSource(new CreateSourceBody(new Uri("https://example.test/feed.xml"), true));
    foreach (var state in new[] { "pending", "completed", "failed" })
    {
      registry[$"native.rss-feeds.create.status.{state}"] = VouchaApiEndpoints.RssFeedImportStatus(ImportId);
    }

    registry["native.rss-feeds.delete.accepted"] = VouchaApiEndpoints.DeleteRssFeed(FeedId);
    registry["native.rss-feeds.delete.retained-import-conflict"] = VouchaApiEndpoints.DeleteRssFeed(FeedId);
    registry["native.topics.merge.accepted"] = VouchaApiEndpoints.MergeTopicAliases(MergeSource, MergeDestination);
    foreach (var state in new[] { "queued", "running", "completed", "failed" })
    {
      registry[$"native.topics.merge.status.{state}"] = VouchaApiEndpoints.TopicMergeStatus(MergeSource, MergeOperation);
    }
  }

  private static ApiRequest TimeRangePosts(string timeRange) =>
      VouchaApiEndpoints.AllPosts(new FetchPostsRequest(Sort: "new", TimeRange: timeRange));

  private static ApiRequest Personal(string timeRange, string? after = null, int limit = 25) =>
      VouchaApiEndpoints.RssFeedItems("all", after, limit, timeRange: timeRange);

  private static string After(string fixtureId) => ApiFixtureLoader.QueryValue(fixtureId, "after");
}
