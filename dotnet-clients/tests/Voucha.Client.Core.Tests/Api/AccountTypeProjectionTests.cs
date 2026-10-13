using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class AccountTypeProjectionTests
{
  [Theory]
  [InlineData("official", AccountType.Official)]
  [InlineData("system", AccountType.System)]
  [InlineData("ai_agent", AccountType.AiAgent)]
  [InlineData(null, null)]
  public void StagedIdentityAndPublicUserProjectionsDecodeCurrentAccountType(
      string? wireValue, AccountType? expected)
  {
    var identityEnvelope = JsonNode.Parse(ApiFixtureLoader.LoadResponse("swift.my.identity.default"))!;
    var identity = identityEnvelope["identity"]!;
    Assert.Null(identity["is_official_account"]);
    Assert.Null(identity["is_agent"]);
    identity["account_type"] = wireValue is null ? null : JsonValue.Create(wireValue);

    var privateUser = identity.Deserialize<User>(VouchaApiJson.Options)!;
    Assert.Equal(expected, privateUser.AccountType);

    var publicEnvelope = JsonNode.Parse(ApiFixtureLoader.LoadResponse("native.users.profile.default"))!;
    var publicUserNode = publicEnvelope["user"]!;
    publicUserNode["account_type"] = wireValue is null ? null : JsonValue.Create(wireValue);

    var publicUser = publicUserNode.Deserialize<PublicUser>(VouchaApiJson.Options)!;
    var searchResult = publicUserNode.Deserialize<UserSearchResult>(VouchaApiJson.Options)!;
    Assert.Equal(expected, publicUser.AccountType);
    Assert.Equal(expected, searchResult.AccountType);
  }

  [Theory]
  [InlineData("official", AccountType.Official)]
  [InlineData("system", AccountType.System)]
  [InlineData("ai_agent", AccountType.AiAgent)]
  [InlineData(null, null)]
  public void StagedPostsFeedPreservesEmbeddedAuthorAccountType(string? wireValue, AccountType? expected)
  {
    var envelope = JsonNode.Parse(ApiFixtureLoader.LoadResponse("swift.posts.feed.default"))!;
    envelope["users"]!["user-1"]!["account_type"] =
        wireValue is null ? null : JsonValue.Create(wireValue);

    var response = envelope.Deserialize<PostsFeedResponse>(VouchaApiJson.Options)!;
    Assert.Equal(expected, response.Users!["user-1"].AccountType);
  }

  [Theory]
  [InlineData("official", AccountType.Official)]
  [InlineData("system", AccountType.System)]
  [InlineData("ai_agent", AccountType.AiAgent)]
  [InlineData(null, null)]
  public void StagedCommunityAndReferralUserProjectionsDecodeAccountType(
      string? wireValue, AccountType? expected)
  {
    var community = StagedUser("web.communities.search.default", wireValue);
    var owner = community.Deserialize<CommunityOwner>(VouchaApiJson.Options)!;
    Assert.Equal(expected, owner.AccountType);

    var referral = StagedUser("web.referral-links.feed.default", wireValue);
    var feedUser = referral.Deserialize<ReferralLinkFeedUser>(VouchaApiJson.Options)!;
    Assert.Equal(expected, feedUser.AccountType);

    var click = StagedUser("native.referral-clicks.mine.default", wireValue);
    var clickUser = click.Deserialize<User>(VouchaApiJson.Options)!;
    Assert.Equal(expected, clickUser.AccountType);
  }

  private static JsonNode StagedUser(string fixture, string? wireValue)
  {
    var response = JsonNode.Parse(ApiFixtureLoader.LoadResponse(fixture))!;
    var users = response["users"]!.AsObject();
    var first = users.OrderBy(entry => entry.Key, StringComparer.Ordinal).First().Value!.DeepClone();
    first["account_type"] = wireValue is null ? null : JsonValue.Create(wireValue);
    return first;
  }
}
