using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class CopyrightMediaContractTests
{
  [Fact]
  public void PostImagePlacementFixturePreservesPlacementIdentity()
  {
    var response = JsonSerializer.Deserialize<PostImagePlacementResponse>(
        ApiFixtureLoader.LoadResponse("native.posts.images.placement.default"),
        VouchaApiJson.Options);

    var image = Assert.Single(Assert.NotNull(response).Images);
    Assert.Equal("00000000-0000-7000-8000-000000000803", image.PlacementId);
    Assert.Equal(1, image.PlacementRevision);
    Assert.Equal("00000000-0000-7000-8000-000000000802", image.ImageId);
  }

  [Fact]
  public void SimilarityCandidateFixturePreservesAvailabilityAndPlacementIdentity()
  {
    var response = JsonSerializer.Deserialize<CopyrightImageSimilarityCandidatesResponse>(
        ApiFixtureLoader.LoadResponse("native.moderation.copyright.image-similarity-candidates.default"),
        VouchaApiJson.Options);

    var candidate = Assert.Single(Assert.NotNull(response).CopyrightImageSimilarityCandidates);
    Assert.Equal(CopyrightImageSimilarityAvailability.Available, response.Availability);
    Assert.Equal("00000000-0000-7000-8000-000000000803", candidate.PlacementId);
    Assert.Equal(1, candidate.PlacementRevision);
    Assert.Equal(0.98, candidate.Similarity, 6);
  }

  [Fact]
  public void CopyrightMediaEndpointsEscapeEveryPathSegment()
  {
    Assert.Equal("/api/v1/posts/post%20%2F%20one/images", VouchaApiEndpoints.PostImages("post / one").Path);
    Assert.Equal(
        "/api/v1/copyright-notices/notice%20%2F%20one/targets/target%20%2F%20two/image-similarity-candidates",
        VouchaApiEndpoints.CopyrightImageSimilarityCandidates("notice / one", "target / two").Path);
  }

  [Fact]
  public void PlacementImageUrlUsesTheRevisionBoundDeliveryPath()
  {
    var config = new Voucha.Client.Core.AppConfig(
        new Uri("https://api.example.test"),
        ImageBaseUrl: new Uri("https://images.example.test/assets"));

    Assert.Equal(
        new Uri("https://images.example.test/assets/images/placements/placement%20%2F%20one/0/image%20%2F%20two?w=960"),
        config.ImageUrlForPlacement("placement / one", 0, "image / two", 960));
    Assert.Null(config.ImageUrlForPlacement("", 1, "image"));
  }
}
