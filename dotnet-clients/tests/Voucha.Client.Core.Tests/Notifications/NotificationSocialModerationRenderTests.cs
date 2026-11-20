using Voucha.Client.Core.Notifications;
using Xunit;
using static Voucha.Client.Core.Tests.Notifications.NotificationRenderSupport;

namespace Voucha.Client.Core.Tests.Notifications;

public sealed class NotificationSocialModerationRenderTests
{
  private const string Actors = "\"actors\":{\"u1\":{\"id\":\"u1\",\"username\":\"alice\"}}";
  private const string Actor = "\"actor_user_id\":\"u1\"";

  [Fact]
  public void FollowKindsNameTheActorOrAreAnonymous()
  {
    Assert.Equal(("@alice started following you", ""), Render("follow", Actor, Actors));
    Assert.Equal(("Someone started following you", ""), Render("follow"));
    Assert.Equal(("Your referral @alice signed up and followed you!", ""), Render("follow_referral_signup", Actor, Actors));
    Assert.Equal(("Your referral signed up and followed you!", ""), Render("follow_referral_signup"));
  }

  [Theory]
  [InlineData("1", "You now have 1 referral")]
  [InlineData("2", "You now have 2 referrals")]
  [InlineData("0", "You now have 0 referrals")]
  [InlineData("\"1\"", "You now have 1 referral")]
  [InlineData("true", "You now have 1 referral")]
  [InlineData("\"many\"", "You now have 0 referrals")]
  [InlineData("null", "You now have 0 referrals")]
  public void ReferralSignupBodyFollowsTheReferralCount(string count, string body)
  {
    Assert.Equal(
        ("@alice signed up through your referral!", body),
        Render("referral_signup", $"{Actor},\"params\":{{\"referral_count\":{count}}}", Actors));
  }

  [Fact]
  public void ReferralSignupIsAnonymousWithoutParams()
  {
    Assert.Equal(("Someone signed up through your referral!", "You now have 0 referrals"), Render("referral_signup"));
  }

  [Theory]
  [InlineData("referral_click", "Someone clicked your referral link")]
  [InlineData("direct_message", "New direct message")]
  [InlineData("modmail", "New modmail message")]
  [InlineData("critical_moderation_alert", "Critical moderation alert")]
  public void SocialTitleOnlyKinds(string kind, string title)
  {
    Assert.Equal((title, ""), Render(kind));
  }

  [Fact]
  public void HouseholdInvitationHasTitleAndBody()
  {
    Assert.Equal(
        ("Household invitation", "You have been invited to join a household."),
        Render("household_invitation"));
  }

  [Theory]
  [InlineData("report_reviewed", "Your report was reviewed", "Thanks for helping keep Voucha safe.")]
  [InlineData("review_actioned_note", "Your review has been actioned by a moderator", "A moderator has added a contextual note to your review following a dispute.")]
  [InlineData("review_actioned_removed", "Your review has been actioned by a moderator", "A moderator has removed your review following a dispute. You may contact support if you believe this was in error.")]
  [InlineData("community_ban", "You have been banned from a community", "A moderator has banned you from a community. You may file an appeal if you believe this was in error.")]
  public void ModerationFixedCopy(string kind, string title, string body)
  {
    Assert.Equal((title, body), Render(kind));
  }

  [Theory]
  [InlineData("review_dispute_resolved", "review_dispute_id", "Your review dispute has been resolved")]
  [InlineData("moderation_appeal_resolved", "moderation_appeal_id", "Your moderation appeal has been resolved")]
  public void ResolutionBodyIsTheModeratorText(string kind, string ownerField, string title)
  {
    const string Texts = "\"public_texts\":{\"d1\":\"We decided <this>.\"}";
    Assert.Equal((title, "We decided <this>."), Render(kind, $"\"{ownerField}\":\"d1\"", Texts));
    Assert.Equal((title, ""), Render(kind, $"\"{ownerField}\":\"missing\"", Texts));
    Assert.Equal((title, ""), Render(kind));
  }

  [Fact]
  public void UserWarningUsesModeratorTextWithFixedFallback()
  {
    const string Texts = "\"public_texts\":{\"w1\":\"Be kind.\",\"w2\":\"\"}";
    Assert.Equal(("You received a warning", "Be kind."), Render("user_warning", "\"user_warning_id\":\"w1\"", Texts));
    Assert.Equal(
        ("You received a warning", "A moderator has issued a warning to your account."),
        Render("user_warning", "\"user_warning_id\":\"w2\"", Texts));
    Assert.Equal(
        ("You received a warning", "A moderator has issued a warning to your account."),
        Render("user_warning"));
  }

  [Fact]
  public void MissingKindRendersTheGenericRow()
  {
    var response = NotificationRenderSupport.Response("x", "", "");
    var missing = response.Notifications["n"] with { Kind = null };

    Assert.Same(NotificationText.Generic, NotificationRenderer.Render(missing, response, WebBase));
  }

  [Fact]
  public void UnknownKindRendersTheGenericRowWithoutBody()
  {
    Assert.Equal(("New notification", ""), Render("from_the_future", "\"params\":{\"x\":1}"));
  }

  [Fact]
  public void EveryRegisteredKindRendersATitle()
  {
    foreach (var kind in NotificationRenderer.Kinds)
    {
      Assert.NotEqual("New notification", Render(kind).Title);
    }

    Assert.Equal(35, NotificationRenderer.Kinds.Count);
  }
}
