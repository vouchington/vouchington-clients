using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiEndpointParityTests
{
  [Fact]
  public void EmailAddressVerificationEndpointsPreserveWireContract()
  {
    var request = VouchaApiEndpoints.RequestEmailAddressVerification(" User@Example.com ");
    var verify = VouchaApiEndpoints.VerifyEmailAddress("user@example.com", "ABCD1234");

    Assert.Equal(HttpMethod.Post, request.Method);
    Assert.Equal("/api/v1/my/email-addresses", request.Path);
    Assert.Equal("{\"email_address\":\" User@Example.com \"}", JsonSerializer.Serialize(request.Body, VouchaApiJson.Options));
    Assert.Equal("/api/v1/my/email-addresses/user%40example.com/verifications", verify.Path);
    Assert.Equal("{\"token\":\"ABCD1234\"}", JsonSerializer.Serialize(verify.Body, VouchaApiJson.Options));
  }

  [Fact]
  public void RequestEmailOtpBodySerializesUiLocaleWhenProvided()
  {
    var body = new RequestEmailOtpBody("tests@example.com", "turnstile-token", "fr");

    var json = JsonSerializer.Serialize(body, VouchaApiJson.Options);

    Assert.Contains("\"emailAddress\":\"tests@example.com\"", json, StringComparison.Ordinal);
    Assert.Contains("\"cfTurnstileResponse\":\"turnstile-token\"", json, StringComparison.Ordinal);
    Assert.Contains("\"ui_locale\":\"fr\"", json, StringComparison.Ordinal);
  }

  [Fact]
  public void UpdateUserPrivacyBodySerializesThePrivacyFormFields()
  {
    var body = new UpdateUserPrivacyBody(
        FollowsVisibility: "followers",
        TopicFollowsVisibility: "mutual_followers",
        RssFeedFollowsVisibility: "everyone",
        CommunityMembershipsVisibility: "users",
        FollowersVisibility: "nobody",
        LikesVisibility: "followers",
        DirectMessagesAudience: "mutual_followers",
        CardsVisibility: "everyone",
        RewardsProgramStatusesVisibility: "users",
        SpendingCategoriesVisibility: "nobody",
        DefaultPostBroadcast: "followers",
        DefaultPostPrivacy: "private",
        UiLocale: JsonNullableString.FromString("fr"),
        ProcessingRestrictedAt: true,
        ThirdPartyMarketing: false,
        HnDiscussions: true);

    var json = JsonSerializer.Serialize(body, VouchaApiJson.Options);

    Assert.Contains("\"follows_visibility\":\"followers\"", json, StringComparison.Ordinal);
    Assert.Contains("\"topic_follows_visibility\":\"mutual_followers\"", json, StringComparison.Ordinal);
    Assert.Contains("\"rss_feed_follows_visibility\":\"everyone\"", json, StringComparison.Ordinal);
    Assert.Contains("\"community_memberships_visibility\":\"users\"", json, StringComparison.Ordinal);
    Assert.Contains("\"followers_visibility\":\"nobody\"", json, StringComparison.Ordinal);
    Assert.Contains("\"likes_visibility\":\"followers\"", json, StringComparison.Ordinal);
    Assert.Contains("\"direct_messages_audience\":\"mutual_followers\"", json, StringComparison.Ordinal);
    Assert.Contains("\"cards_visibility\":\"everyone\"", json, StringComparison.Ordinal);
    Assert.Contains("\"rewards_program_statuses_visibility\":\"users\"", json, StringComparison.Ordinal);
    Assert.Contains("\"spending_categories_visibility\":\"nobody\"", json, StringComparison.Ordinal);
    Assert.Contains("\"default_post_broadcast\":\"followers\"", json, StringComparison.Ordinal);
    Assert.Contains("\"default_post_privacy\":\"private\"", json, StringComparison.Ordinal);
    Assert.Contains("\"ui_locale\":\"fr\"", json, StringComparison.Ordinal);
    Assert.Contains("\"processing_restricted_at\":true", json, StringComparison.Ordinal);
    Assert.Contains("\"third_party_marketing\":false", json, StringComparison.Ordinal);
    Assert.Contains("\"hn_discussions\":true", json, StringComparison.Ordinal);
  }
}
