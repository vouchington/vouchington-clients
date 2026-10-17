using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Friends;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Friends;

public sealed class FriendRecommendationProviderTests
{
  [Theory]
  [InlineData("facebook", FriendRecommendationProvider.Facebook, "via Facebook")]
  [InlineData("x", FriendRecommendationProvider.X, "via X")]
  [InlineData("github", FriendRecommendationProvider.Github, "via GitHub")]
  public void AdvertisedProvidersDeserializeAndPresent(
      string wireProvider, FriendRecommendationProvider provider, string expectedPresentation)
  {
    var response = JsonSerializer.Deserialize<FriendRecommendationsResponse>(
        ResponseWithProvider(wireProvider), VouchaApiJson.Options)!;
    var recommendation = Assert.Single(response.Results);
    Assert.Equal(provider, recommendation.Provider);
    Assert.Equal(expectedPresentation, new FriendRecommendationRow(recommendation, null).ProviderPresentation);
  }

  [Theory]
  [InlineData("microsoft")]
  [InlineData("linkedin")]
  [InlineData("unknown-provider")]
  public void UnadvertisedProvidersAreRejectedBeforePresentation(string provider)
  {
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<FriendRecommendationsResponse>(
        ResponseWithProvider(provider), VouchaApiJson.Options));
  }

  private static string ResponseWithProvider(string provider)
  {
    var response = JsonNode.Parse(ApiFixtureLoader.LoadResponse("native.friend-recommendations.default"))!;
    response["results"]![0]!["provider"] = provider;
    return response.ToJsonString();
  }
}
