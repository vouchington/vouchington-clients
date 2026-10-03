using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class ApiModelsContentTests
{
  [Fact]
  public void PostAppendsNewOptionalParametersForPositionalCompatibility()
  {
    var parameterNames = typeof(Post)
        .GetConstructors()
        .Single()
        .GetParameters()
        .Select(parameter => parameter.Name!)
        .ToArray();

    Assert.Equal(
        ["ApprovedAt", "InReviewAt", "RejectedAt", "PostExplicitCategories", "PostHashtags",
         "DeclaredLanguage", "LinguaRsDetectedLanguage", "ContentProvenance"],
        parameterNames[^8..]);
  }

  [Fact]
  public void PostDecodesOptionalPublicContentProvenance()
  {
    const string withLabel = """
        {"id":"post-1","post_type":"discussion","title":"Title","markdown":"Body",
         "created_by_id":"user-1","content_provenance":{"via":"mcp","label":"via Example"}}
        """;
    const string withoutLabel = """
        {"id":"post-2","post_type":"discussion","title":"Title","markdown":"Body",
         "created_by_id":"user-1"}
        """;

    var labeled = JsonSerializer.Deserialize<Post>(withLabel);
    var unlabeled = JsonSerializer.Deserialize<Post>(withoutLabel);

    Assert.Equal("mcp", labeled?.ContentProvenance?.Via);
    Assert.Equal("via Example", labeled?.ContentProvenance?.Label);
    Assert.Null(unlabeled?.ContentProvenance);
  }

  [Fact]
  public void ContentApiModelsExposeOptionalDefaultsAndConstructedValues()
  {
    var now = DateTimeOffset.Parse("2026-06-28T10:00:00Z");
    var data = new RssFeedItemData(
        "Episode 1",
        new Uri("https://example.com/episode"),
        "Summary",
        Creator: "Host");
    var feed = new RssFeedItemFeed("feed-1", "Show", "podcast");
    var mediaContent = new RssFeedMediaContent(
        3600,
        "audio",
        "audio/mpeg",
        new Uri("https://cdn.example.com/episode.mp3"));
    var item = new RssFeedItem(
        "item-1",
        data,
        new ApiUrl(new Uri("https://example.com/item")),
        feed,
        "audio",
        now,
        [],
        "Episode 1",
        new Uri("https://example.com/episode"),
        mediaContent,
        "feed-1");

    Assert.Equal("Episode 1", data.Title);
    Assert.Equal(new Uri("https://example.com/episode"), data.Link);
    Assert.Equal("Summary", data.ContentSnippet);
    Assert.Equal("Host", data.Creator);
    Assert.Null(data.EnclosureType);
    Assert.Null(data.EnclosureLength);
    Assert.Null(data.DurationSeconds);
    Assert.Null(data.VideoId);
    Assert.Null(data.VideoPlatform);

    Assert.Equal("feed-1", feed.Id);
    Assert.Equal("Show", feed.Title);
    Assert.Equal("podcast", feed.FeedType);
    Assert.Null(feed.Topic);
    Assert.Null(feed.PodcastShow);
    Assert.Null(feed.RssFeedUrl);
    Assert.Null(feed.HomePageUrl);
    Assert.Null(feed.Hostname);
    Assert.Null(feed.LastFetchedAt);
    Assert.Null(feed.IsEnabled);

    Assert.Equal("item-1", item.Id);
    Assert.Same(data, item.Data);
    Assert.Equal("audio", item.MediaType);
    Assert.Equal(now, item.PublishedAt);
    Assert.NotNull(item.RssFeedSources);
    Assert.Empty(item.RssFeedSources!);
    Assert.Equal("Episode 1", item.Title);
    Assert.Equal(new Uri("https://example.com/episode"), item.Link);
    Assert.Same(mediaContent, item.MediaContent);
    Assert.Equal("feed-1", item.RssFeedId);
    Assert.Null(item.Categories);
    Assert.Null(item.Content);
    Assert.Null(item.Creator);
    Assert.Null(item.Description);
  }

  [Fact]
  public void PostDetailResponseExposesAllSidecars()
  {
    var post = new Post("post-1", "review", "Review", "Body", "user-1");
    var metrics = new PostMetrics(Count: new PostMetricCounts(1));
    var authorAside = new PostDetailAuthorAside("About", true, []);
    var electionVote = new ElectionVote("election_vote", "post-1", "user-1", ElectionVoteChoice.Like, DateTimeOffset.UnixEpoch);
    var postElection = new PostElection("post_election", "post-1", 2, 1, 0);
    var bookmarks = new Dictionary<string, IReadOnlyDictionary<string, bool>>(StringComparer.Ordinal)
    {
      ["post-1"] = new Dictionary<string, bool>(StringComparer.Ordinal) { ["save"] = true },
    };

    var response = new PostDetailResponse(
        authorAside,
        bookmarks,
        electionVote,
        "<p>Body</p>",
        post,
        postElection,
        metrics);

    Assert.Same(authorAside, response.AuthorAside);
    Assert.Same(bookmarks, response.Bookmarks);
    Assert.Same(electionVote, response.ElectionVote);
    Assert.Equal("<p>Body</p>", response.Html);
    Assert.Same(post, response.Post);
    Assert.Same(postElection, response.PostElection);
    Assert.Same(metrics, response.PostMetrics);
    Assert.Null(post.ApprovedAt);
    Assert.Null(post.InReviewAt);
    Assert.Null(post.RejectedAt);
  }

  [Fact]
  public void PostDeserializesExplicitTopicAndHashtagCategories()
  {
    var post = JsonSerializer.Deserialize<Post>("""
      {
        "id": "post-1",
        "post_type": "discussion",
        "title": "Fixture post",
        "markdown": "#Travel",
        "created_by_id": "user-1",
        "post_explicit_categories": [
          { "type": "topic", "topic_id": "topic-1", "topic_name": "Travel" },
          { "type": "hashtag", "hashtag": "#Travel" }
        ],
        "post_hashtags": [
          { "id": "hashtag-1", "key": "travel", "display_token": "#Travel", "topic_id": "topic-1" }
        ]
      }
      """)!;

    Assert.Collection(
        post.PostExplicitCategories!,
        topic =>
        {
          Assert.Equal("topic", topic.Type);
          Assert.Equal("topic-1", topic.TopicId);
          Assert.Equal("Travel", topic.TopicName);
          Assert.Null(topic.Hashtag);
        },
        hashtag =>
        {
          Assert.Equal("hashtag", hashtag.Type);
          Assert.Equal("#Travel", hashtag.Hashtag);
          Assert.Null(hashtag.TopicId);
        });
    var hashtag = Assert.Single(post.PostHashtags!);
    Assert.Equal(("hashtag-1", "travel", "#Travel", "topic-1"),
        (hashtag.Id, hashtag.Key, hashtag.DisplayToken, hashtag.TopicId));
  }

  [Fact]
  public void AuthoredContentTextDecodesOnlyWhenItsRequiredFieldsArePresent()
  {
    var content = JsonSerializer.Deserialize<AuthoredContentText>("""
      {"kind":"post","text":"Original content","declared_language":null,"lingua_rs_detected_language":"en"}
      """, VouchaApiJson.Options);

    Assert.Equal("post", content?.Kind);
    Assert.Equal("Original content", content?.Text);
    Assert.Equal("en", content?.LinguaRsDetectedLanguage);
    Assert.Null(JsonSerializer.Deserialize<AuthoredContentText?>("null", VouchaApiJson.Options));
  }

  [Theory]
  [InlineData("{}")]
  [InlineData("{\"kind\":\"post\"}")]
  [InlineData("{\"text\":\"Original content\"}")]
  [InlineData("{\"kind\":null,\"text\":\"Original content\"}")]
  [InlineData("{\"kind\":\"post\",\"text\":null}")]
  public void AuthoredContentTextRejectsMissingOrNullRequiredFields(string json)
  {
    Assert.ThrowsAny<ArgumentNullException>(() =>
        JsonSerializer.Deserialize<AuthoredContentText>(json, VouchaApiJson.Options));
  }
}
