using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class NativeContentModelsTests
{
  private static T Load<T>(string fixtureId) =>
      JsonSerializer.Deserialize<T>(ApiFixtureLoader.LoadResponse(fixtureId), VouchaApiJson.Options)!;

  [Fact]
  public void PostDetailFixtureCarriesHackerNewsStoryAndCommentReferences()
  {
    var response = Load<PostResponse>("native.posts.hacker-news.detail");

    var entry = Assert.Single(response.PostHackerNewsReferences!);
    Assert.Equal("70000000-0000-7000-8000-000000005400", entry.Key);
    var references = entry.Value;
    Assert.Equal(2, references.CommentCount);
    Assert.Equal(1, references.StoryCount);
    Assert.Equal([303L, 302L], references.Comments!.Select(c => c.HackerNewsItemId));
    Assert.All(references.Comments!, c => Assert.Equal(301L, c.ParentStoryItemId));
    Assert.Equal(DateTimeOffset.Parse("2026-01-01T02:00:00Z"), references.Comments![0].PostedAt);
    var story = Assert.Single(references.Stories!);
    Assert.Equal(301L, story.HackerNewsItemId);
    Assert.Equal(12, story.Points);
    Assert.Equal(4, story.ReplyCount);
    Assert.Equal(DateTimeOffset.Parse("2026-01-01T00:00:00Z"), story.PostedAt);
  }

  [Fact]
  public void RssFeedItemsFixtureCarriesCountOnlyHackerNewsReferences()
  {
    var response = Load<RssFeedItemsFeedResponse>("native.rss-feed-items.hacker-news.list");

    var references = response.RssFeedItemHackerNewsReferences!["item-1"];
    Assert.Equal(2, references.CommentCount);
    Assert.Equal(1, references.StoryCount);
    Assert.Null(references.Comments);
    Assert.Null(references.Stories);
    Assert.Empty(response.RssFeedBookmarks!);
    Assert.Empty(response.RssFeedItemContentHtml!);
  }

  [Fact]
  public void RssFeedItemsResponseMapsBookmarkAndContentHtmlSideTables()
  {
    var body = JsonNode.Parse(ApiFixtureLoader.LoadResponse("native.rss-feed-items.hacker-news.list"))!.AsObject();
    body["rss_feed_bookmarks"] = JsonNode.Parse("{\"item-1\":{\"save\":true}}");
    body["rss_feed_item_content_html"] = JsonNode.Parse("{\"item-1\":\"<p>Hello</p>\"}");

    var response = JsonSerializer.Deserialize<RssFeedItemsFeedResponse>(body.ToJsonString(), VouchaApiJson.Options)!;

    Assert.True(response.RssFeedBookmarks!["item-1"].Save);
    Assert.Equal("<p>Hello</p>", response.RssFeedItemContentHtml!["item-1"]);
  }

  [Fact]
  public void TopicMergeAcceptedFixtureMapsQueuedDetails()
  {
    var merge = Load<TopicMergeResponse>("native.topics.merge.accepted").TopicMerge;

    Assert.Equal("70000000-0000-7000-8000-000000005172", merge.Id);
    Assert.Equal("70000000-0000-7000-8000-000000005170", merge.SourceTopicId);
    Assert.Equal("70000000-0000-7000-8000-000000005171", merge.DestinationTopicId);
    Assert.Equal("queued", merge.Status);
    Assert.Equal("preflight", merge.Stage);
    Assert.Null(merge.ErrorCode);
    Assert.Null(merge.CompletedAt);
    Assert.Equal(DateTimeOffset.Parse("2026-11-01T00:00:00Z"), merge.CreatedAt);
    Assert.Equal(
        "/api/v1/topics/70000000-0000-7000-8000-000000005170/merges/70000000-0000-7000-8000-000000005172",
        merge.StatusUrl);
  }

  [Fact]
  public void TopicMergeCompletedFixtureMapsCompletionTime()
  {
    var merge = Load<TopicMergeResponse>("native.topics.merge.status.completed").TopicMerge;

    Assert.Equal("completed", merge.Status);
    Assert.Equal("cleanup", merge.Stage);
    Assert.Equal(DateTimeOffset.Parse("2026-11-01T00:01:00Z"), merge.CompletedAt);
  }

  [Fact]
  public void ReportJudgementRunEndedDefaultsToFailedAndExpiredMapsToExpiredKey()
  {
    var failed = new ReportJudgementRunEndedException();
    var expired = new ReportJudgementRunEndedException(ModerationRunStatus.Expired);

    Assert.Equal(ModerationRunStatus.Failed, failed.Status);
    Assert.Equal(UiMessageKey.NativeSwiftModerationReportsAiJudgementRunFailed, failed.Key);
    Assert.Equal(ModerationRunStatus.Expired, expired.Status);
    Assert.Equal(UiMessageKey.NativeSwiftModerationReportsAiJudgementRunExpired, expired.Key);
  }

  [Fact]
  public void ReportJudgementRunEndedMessageConstructorsKeepMessageAndInnerException()
  {
    var withMessage = new ReportJudgementRunEndedException("run ended");
    var inner = new InvalidOperationException("boom");
    var withInner = new ReportJudgementRunEndedException("wrapped", inner);

    Assert.Equal("run ended", withMessage.Message);
    Assert.Equal(ModerationRunStatus.Failed, withMessage.Status);
    Assert.Equal("wrapped", withInner.Message);
    Assert.Same(inner, withInner.InnerException);
    Assert.Equal(UiMessageKey.NativeSwiftModerationReportsAiJudgementRunFailed, withInner.Key);
  }
}
