using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class StaffProvenanceContractTests
{
  [Fact]
  public void StaffFixturesPreserveRawChannelsAndVerifiedOAuthClientAcrossEntities()
  {
    var communities = Read<CommunitySearchResponse>("communities.staff");
    var topics = Read<TopicSearchResponse>("topics.staff");
    var lists = Read<ListsSearchResponse>("lists.staff");
    var feeds = Read<RssFeedsResponse>("rss-feeds.staff");

    Assert.Equal("web", communities.Communities["community-1-web"].StaffProvenance?.CreatedVia);
    Assert.Equal("api", topics.Topics["topic-1-api"].StaffProvenance?.CreatedVia);
    Assert.Equal("api", lists.Lists["list-1-api"].StaffProvenance?.CreatedVia);
    var client = communities.Communities["community-1-mcp"].StaffProvenance?.OAuthClient;
    Assert.Equal("voucha_fixture_agent", client?.ClientId);
    Assert.Equal("Fixture Agent", client?.ClientName);
    Assert.True(client?.Verified);
    Assert.Null(client?.MetadataUrl);
    Assert.Contains(feeds.Results, feed => feed.StaffProvenance?.OAuthClient?.Verified == true);
  }

  [Fact]
  public void PublicFixtureDoesNotInventStaffFacts()
  {
    var communities = Read<CommunitySearchResponse>("communities");
    Assert.All(communities.Communities.Values, community => Assert.Null(community.StaffProvenance));
  }

  [Theory]
  [InlineData(false, false)]
  [InlineData(true, false)]
  [InlineData(true, true)]
  public void RawClientNameAndIdRequireStaffDetail(bool isStaffViewer, bool showClient)
  {
    var facts = new StaffContentProvenance("mcp",
        new StaffProvenanceOAuthClient("internal-client-42", "Fixture Agent", null, false));
    var channel = StaffProvenanceLabels.Channel(facts, isStaffViewer, UiLocalization.English);
    var client = StaffProvenanceLabels.Client(facts, isStaffViewer, showClient, UiLocalization.English);
    var verification = StaffProvenanceLabels.Verification(facts, isStaffViewer, showClient, UiLocalization.English);

    Assert.Equal(isStaffViewer, channel is not null);
    Assert.Equal(isStaffViewer && showClient, client is not null);
    Assert.Equal(isStaffViewer && showClient, verification is not null);
    if (client is not null)
    {
      Assert.Contains("Fixture Agent", client, StringComparison.Ordinal);
      Assert.Contains("internal-client-42", client, StringComparison.Ordinal);
    }
  }

  private static T Read<T>(string entity) =>
      JsonSerializer.Deserialize<T>(File.ReadAllText(FilamentsContractPaths.ApiFixture(
          $"responses/entity-provenance.{entity}.json")), VouchaApiJson.Options)
      ?? throw new InvalidOperationException($"Missing {entity} fixture response.");
}
