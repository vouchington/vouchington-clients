using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Lists;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Topics;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class PublicEntityProvenanceSurfaceTests
{
  [Fact]
  public void CanonicalPublicFixturesCarryOnlyTrustedLabelsIntoEntityRows()
  {
    var communities = Read<CommunitySearchResponse>("communities");
    var topics = Read<TopicSearchResponse>("topics");
    var lists = Read<ListsSearchResponse>("lists");
    var feeds = Read<RssFeedsResponse>("rss-feeds");

    AssertLabels(communities.Communities.Values.Select(community =>
        CommunityBrowseRow.FromCommunity(community, null, null).LocalizedProvenanceLabel));
    AssertLabels(topics.Topics.Values.Select(topic =>
        PublicProvenanceLabels.Resolve(topic.Provenance, UiLocalization.English)));
    AssertLabels(lists.Lists.Values.Select(list =>
        ListSummaryRow.FromList(list).LocalizedProvenanceLabel));
    AssertLabels(feeds.Results.Select(feed =>
        PublicProvenanceLabels.Resolve(feed.Provenance, UiLocalization.English)));
  }

  [Fact]
  public async Task SourceFetchCarriesProvenanceWhileUnrelatedFeedRowsStayUnlabeled()
  {
    var handler = new RecordingHandler(File.ReadAllText(FilamentsContractPaths.ApiFixture(
        "responses/entity-provenance.rss-feeds.json")));
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var service = new ApiNewsFeedService(client);
    var rows = await service.GetNewsFeedItemsAsync(NewsFeedScope.AllSources, TestContext.Current.CancellationToken);
    var articleRow = new NewsFeedItem("article", "Article", "Source", "", null, DateTimeOffset.UnixEpoch);

    Assert.Equal("/api/v1/rss-feeds?feed_type=article&limit=25", handler.PathAndQuery);
    Assert.Equal(3, rows.Count);
    Assert.Contains(rows, row => row.Id.EndsWith("-mcp", StringComparison.Ordinal) &&
        row.LocalizedProvenanceLabel?.Contains("Fixture Agent", StringComparison.Ordinal) == true);
    Assert.Contains(rows, row => row.Id.EndsWith("-web", StringComparison.Ordinal) && !row.HasProvenance);
    Assert.False(articleRow.HasProvenance);
  }

  [Fact]
  public async Task SearchRowsPreservePublicProvenanceThroughCommunityAndTopicServices()
  {
    var communityHandler = new RecordingHandler(File.ReadAllText(FilamentsContractPaths.ApiFixture(
        "responses/entity-provenance.communities.json")));
    var communityClient = new VouchaApiClient(new HttpClient(communityHandler)
    { BaseAddress = new Uri("https://api.test") });
    using var communities = new CommunityBrowseViewModel(new ApiCommunitiesService(communityClient));
    communities.Query = "test";
    await communities.SearchAsync(TestContext.Current.CancellationToken);

    var topicHandler = new RecordingHandler(File.ReadAllText(FilamentsContractPaths.ApiFixture(
        "responses/entity-provenance.topics.json")));
    var topicClient = new VouchaApiClient(new HttpClient(topicHandler)
    { BaseAddress = new Uri("https://api.test") });
    using var topics = new TopicsViewModel(new ApiTopicsService(topicClient));
    await topics.SearchAsync("test", TestContext.Current.CancellationToken);

    AssertLabels(communities.Results.Select(row => row.LocalizedProvenanceLabel));
    AssertLabels(topics.Items.Select(row => row.LocalizedProvenanceLabel));
    Assert.Equal("/api/v1/communities?q=test", communityHandler.PathAndQuery);
    Assert.Equal("/api/v1/topics?q=test", topicHandler.PathAndQuery);
  }

  [Fact]
  public void UnknownKnownAppKeyNeverBecomesAnEntityBadge()
  {
    var row = new CommunityBrowseRow("community-1", "Community", "community", 0, 0, false,
        new PublicContentProvenance("mcp", new PublicProvenanceApp("known", Key: "private-catalog-key")));

    Assert.Equal(PublicProvenanceLabels.Resolve(new("mcp", null), UiLocalization.English),
        row.LocalizedProvenanceLabel);
    Assert.DoesNotContain("private-catalog-key", row.LocalizedProvenanceLabel, StringComparison.Ordinal);
  }

  private static T Read<T>(string entity) =>
      JsonSerializer.Deserialize<T>(File.ReadAllText(FilamentsContractPaths.ApiFixture(
          $"responses/entity-provenance.{entity}.json")), VouchaApiJson.Options)
      ?? throw new InvalidOperationException($"Missing {entity} fixture response.");

  private static void AssertLabels(IEnumerable<string?> labels)
  {
    var values = labels.ToArray();
    Assert.Equal(3, values.Length);
    Assert.Contains(values, label => label?.Contains("Fixture Agent", StringComparison.Ordinal) == true);
    Assert.Contains(values, label => label == PublicProvenanceLabels.Resolve(new("api", null), UiLocalization.English));
    Assert.Contains(values, label => label is null);
    Assert.DoesNotContain(values, label => label?.Contains("voucha_fixture_agent", StringComparison.Ordinal) == true);
  }
}
