using Xunit;
using static Voucha.Client.Core.Tests.Notifications.NotificationRenderSupport;

namespace Voucha.Client.Core.Tests.Notifications;

public sealed class NotificationCommunityRenderTests
{
  private const string Communities = "\"communities\":{\"c1\":{\"id\":\"c1\",\"slug\":\"gardeners\",\"name\":\"Gardeners\"}}";
  private const string Community = "\"community_id\":\"c1\"";

  [Theory]
  [InlineData("community_application_approved", "Your application to Gardeners was approved", "You are now a member of Gardeners.")]
  [InlineData("community_ownership_received", "You now own Gardeners", "Ownership of Gardeners was transferred to you.")]
  [InlineData("community_ownership_transferred_away", "Ownership of Gardeners was transferred", "You transferred ownership of Gardeners.")]
  public void CommunityKindsNameTheCommunity(string kind, string title, string body)
  {
    Assert.Equal((title, body), Render(kind, Community, Communities));
  }

  [Fact]
  public void MissingCommunityFallsBackToTheCatalogName()
  {
    Assert.Equal(
        ("Your application to a community was approved", "You are now a member of a community."),
        Render("community_application_approved", "\"community_id\":\"gone\"", Communities));
    Assert.Equal(
        ("You now own a community", "Ownership of a community was transferred to you."),
        Render("community_ownership_received"));
  }

  [Fact]
  public void RejectedApplicationShowsTheReasonWhenPublished()
  {
    const string Texts = "\"public_texts\":{\"a1\":\"Not enough history.\"}";
    Assert.Equal(
        ("Your application to Gardeners was not approved", "Reason: Not enough history."),
        Render("community_application_rejected", $"{Community},\"community_application_id\":\"a1\"", Communities + "," + Texts));
    Assert.Equal(
        ("Your application to Gardeners was not approved", "Your application to Gardeners was not approved."),
        Render("community_application_rejected", $"{Community},\"community_application_id\":\"a2\"", Communities + "," + Texts));
  }

  [Theory]
  [InlineData("owner", "moderator", "Your community role changed from owner to moderator.")]
  [InlineData("moderator", "member", "Your community role changed from moderator to member.")]
  [InlineData("member", "owner", "Your community role changed from member to owner.")]
  [InlineData("banned", "other", "Your community role changed from member to member.")]
  public void RoleChangeMapsRoleSlugs(string from, string to, string body)
  {
    Assert.Equal(
        ("Your role in Gardeners changed", body),
        Render(
            "community_role_changed",
            $"{Community},\"params\":{{\"from_role\":\"{from}\",\"to_role\":\"{to}\"}}",
            Communities));
  }

  [Fact]
  public void RoleChangeDefaultsMissingRolesToMember()
  {
    Assert.Equal(
        ("Your role in Gardeners changed", "Your community role changed from member to member."),
        Render("community_role_changed", Community, Communities));
  }

  private const string DigestParams =
      "\"params\":{\"community_count\":3,\"joins\":5,\"departures\":2,\"total_members\":1200,\"active_posters\":7,"
      + "\"discussions\":4,\"reviews\":3,\"data_points\":2,\"comments\":11,\"moderation_workload\":1,"
      + "\"top_discussion_replies\":9}";

  [Fact]
  public void DigestComputesNetAndRateAndPluralizesCommunities()
  {
    var (title, body) = Render("community_activity_digest", DigestParams);

    Assert.Equal("Your weekly community activity", title);
    Assert.Equal(
        "3 communities: 5 joined, 2 departed (net 3); 1,200 total members; 4 discussions, 3 reviews, "
        + "2 data points, 11 comments; 7 active members (1%); 1 items awaiting moderation.",
        body);
  }

  [Fact]
  public void DigestUsesSingularCommunityZeroTotalAndNegativeNet()
  {
    var (_, body) = Render(
        "community_activity_digest",
        "\"params\":{\"community_count\":1,\"joins\":0,\"departures\":4,\"total_members\":0,\"active_posters\":5}");

    Assert.Equal(
        "1 community: 0 joined, 4 departed (net -4); 0 total members; 0 discussions, 0 reviews, "
        + "0 data points, 0 comments; 5 active members (0%); 0 items awaiting moderation.",
        body);
  }

  [Fact]
  public void DigestRateRoundsHalfUp()
  {
    var (_, body) = Render(
        "community_activity_digest",
        "\"params\":{\"community_count\":2,\"total_members\":8,\"active_posters\":1}");

    Assert.Contains("1 active members (13%)", body, StringComparison.Ordinal);
  }

  [Fact]
  public void DigestAppendsTheTopDiscussionWhenHydrated()
  {
    const string Hydration = Communities + ",\"posts\":{\"p9\":{\"id\":\"p9\",\"post_type\":\"discussion\","
        + "\"snippet\":\"Best tomatoes\",\"actor_user_id\":null,\"community_id\":\"c1\"}}";
    var (_, body) = Render(
        "community_activity_digest",
        "\"top_post_id\":\"p9\"," + DigestParams,
        Hydration);

    Assert.EndsWith("; top discussion: Gardeners: Best tomatoes (9 replies).", body, StringComparison.Ordinal);
  }

  [Theory]
  [InlineData("\"top_post_id\":\"missing\"", "\"communities\":{}")]
  [InlineData("\"top_post_id\":\"p9\"", "\"posts\":{\"p9\":{\"id\":\"p9\",\"snippet\":\"x\",\"community_id\":null}}")]
  [InlineData("\"top_post_id\":\"p9\"", "\"posts\":{\"p9\":{\"id\":\"p9\",\"snippet\":\"x\",\"community_id\":\"c404\"}}")]
  public void DigestOmitsTopDiscussionWhenPostOrCommunityIsMissing(string notification, string hydration)
  {
    var (_, body) = Render("community_activity_digest", notification + "," + DigestParams, hydration);

    Assert.DoesNotContain("top discussion", body, StringComparison.Ordinal);
  }
}
