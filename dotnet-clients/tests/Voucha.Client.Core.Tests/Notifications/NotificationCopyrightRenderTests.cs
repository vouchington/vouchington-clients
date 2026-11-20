using Xunit;
using static Voucha.Client.Core.Tests.Notifications.NotificationRenderSupport;

namespace Voucha.Client.Core.Tests.Notifications;

public sealed class NotificationCopyrightRenderTests
{
  private const string WorkItem = "\"copyright_notice_delivery_work_item_id\":\"w1\"";
  private const string Texts = "\"public_texts\":{\"w1\":\"A person decided the case.\"}";
  private const string AiAssisted = "\"params\":{\"ai_assisted\":true}";
  private const string AiNote = "Automated tools assisted with processing this case.";

  [Theory]
  [InlineData("copyright_claimant_receipt", "Copyright notice received", "Your copyright notice was received.")]
  [InlineData("copyright_status_update", "Copyright case update", "There is an update to your copyright case.")]
  [InlineData("copyright_poster_restriction_notice", "Material restricted for a copyright notice", "Review the case and available response options.")]
  [InlineData("copyright_poster_review_notice", "Copyright restriction reviewed", "A person reviewed the restriction. See the case page for the decision and reasons.")]
  [InlineData("copyright_poster_restoration_notice", "Copyright restriction ended", "This restriction ended. See the case page for the image availability and reasons.")]
  [InlineData("copyright_owner_information_notice", "Community image restricted", "An image on a community you own was withheld after a copyright notice.")]
  [InlineData("copyright_redress_decision_notice", "Copyright complaint decision", "A person decided your complaint. See the case page for the disposition and reasons.")]
  public void FixedBodyIsTheFallbackWithoutDeliverySummary(string kind, string title, string body)
  {
    Assert.Equal((title, body), Render(kind));
    Assert.Equal((title, body), Render(kind, WorkItem, Texts.Replace("w1", "other", StringComparison.Ordinal)));
  }

  [Theory]
  [InlineData("copyright_claimant_receipt")]
  [InlineData("copyright_status_update")]
  [InlineData("copyright_poster_restriction_notice")]
  [InlineData("copyright_poster_review_notice")]
  [InlineData("copyright_poster_restoration_notice")]
  [InlineData("copyright_owner_information_notice")]
  [InlineData("copyright_redress_decision_notice")]
  [InlineData("copyright_claimant_decision_notice")]
  public void DeliverySummaryReplacesTheFixedBody(string kind)
  {
    Assert.Equal("A person decided the case.", Render(kind, WorkItem, Texts).Body);
  }

  [Fact]
  public void AiAssistedNoteFollowsTheSummaryAndTheFallback()
  {
    Assert.Equal(
        "A person decided the case.\n\n" + AiNote,
        Render("copyright_status_update", WorkItem + "," + AiAssisted, Texts).Body);
    Assert.Equal(
        "There is an update to your copyright case.\n\n" + AiNote,
        Render("copyright_status_update", AiAssisted).Body);
  }

  [Theory]
  [InlineData("false")]
  [InlineData("\"true\"")]
  [InlineData("1")]
  public void OnlyBooleanTrueAddsTheAiAssistedNote(string value)
  {
    Assert.DoesNotContain(
        AiNote,
        Render("copyright_status_update", $"\"params\":{{\"ai_assisted\":{value}}}").Body,
        StringComparison.Ordinal);
  }

  [Theory]
  [InlineData("us_dmca", "Your copyright notice was decided. You may file a new notice, contact the designated agent, or seek judicial redress through a court. New notice: https://voucha.example/copyright/notices/new. Designated agent: https://voucha.example/copyright/designated-agent. Accepted cases show the reasons on the case page.")]
  [InlineData("eu_dsa", "Your copyright notice was decided. The decision statement gives the reasons. You may submit an internal complaint, refer the decision to a certified out-of-court dispute settlement body, or seek judicial redress through a court.")]
  [InlineData("other_law", "Your copyright notice was decided. The decision statement gives the reasons. You may seek judicial redress through a court.")]
  public void ClaimantDecisionFallbackFollowsJurisdiction(string jurisdiction, string body)
  {
    var hydration = $"\"copyright_notices\":{{\"cn1\":{{\"id\":\"cn1\",\"jurisdiction\":\"{jurisdiction}\"}}}}";

    Assert.Equal(
        ("Copyright notice decision", body),
        Render("copyright_claimant_decision_notice", "\"copyright_notice_id\":\"cn1\"", hydration));
  }

  [Fact]
  public void ClaimantDecisionWithoutHydratedNoticeUsesTheOtherJurisdictionBody()
  {
    Assert.Equal(
        "Your copyright notice was decided. The decision statement gives the reasons. You may seek judicial redress through a court.",
        Render("copyright_claimant_decision_notice").Body);
    Assert.StartsWith(
        "Your copyright notice was decided. The decision",
        Render("copyright_claimant_decision_notice", "\"copyright_notice_id\":\"missing\"").Body,
        StringComparison.Ordinal);
  }

  [Fact]
  public void DecisionUrlsUseTheConfiguredWebOrigin()
  {
    var body = Render(
        "copyright_claimant_decision_notice",
        "\"copyright_notice_id\":\"cn1\"",
        "\"copyright_notices\":{\"cn1\":{\"id\":\"cn1\",\"jurisdiction\":\"us_dmca\"}}",
        webBase: new Uri("https://staging.example:8443/app/")).Body;

    Assert.Contains("https://staging.example:8443/copyright/notices/new", body, StringComparison.Ordinal);
    Assert.Contains("https://staging.example:8443/copyright/designated-agent", body, StringComparison.Ordinal);
  }
}
