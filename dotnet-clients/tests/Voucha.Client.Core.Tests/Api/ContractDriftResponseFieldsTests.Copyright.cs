using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class ContractDriftResponseFieldsTests
{
  private static T Fixture<T>(string id) =>
      JsonSerializer.Deserialize<T>(ApiFixtureLoader.LoadResponse(id), VouchaApiJson.Options)
      ?? throw new InvalidOperationException($"Empty fixture {id}.");

  [Fact]
  public void CopyrightAssociationSearchPreservesTheNoticeStaffMustIdentify()
  {
    var response = Fixture<CopyrightAssociationTargetsResponse>("web.copyright.email-notice-association-search");
    var notice = Assert.Single(response.CopyrightNotices);
    Assert.Equal("00000000-0000-7000-8000-000000000841", notice.Id);
    Assert.Equal("Claimant", notice.ClaimantDisplayName);
    Assert.Equal("us_dmca", notice.Jurisdiction);
    Assert.Equal(DateTimeOffset.Parse("2026-10-13T12:00:00Z"), notice.ReceivedAt);
    Assert.Equal("Harbour photograph", notice.WorkDescription);
    Assert.False(response.PageInfo.HasNextPage);

    var account = Assert.Single(Fixture<CopyrightRepeatInfringerAccountsResponse>(
        "native.copyright.repeat-infringer-accounts").Accounts);
    Assert.Equal("00000000-0000-7000-8000-000000000808", account.AccountUserId);
    Assert.Equal("photo-author", account.AccountUsername);
    Assert.Equal("00000000-0000-7000-8000-000000000809", account.IncidentId);
    Assert.True(account.IsOperative);
    Assert.Null(account.OpenReviewId);
    Assert.False(account.TerminationInEffect);
  }

  [Fact]
  public void CopyrightQueuePreservesScreeningGuidanceAndClaimantHistory()
  {
    var response = Fixture<CopyrightStaffQueueResponse>("web.copyright.staff-queue.default");
    Assert.True(response.PageInfo.HasNextPage);
    Assert.Equal("copyright-staff-next", response.PageInfo.EndCursor);
    var notice = response.CopyrightNotices[0];
    Assert.Equal("00000000-0000-7000-8000-000000000804", notice.Id);
    Assert.Equal("us_dmca", notice.Jurisdiction);
    Assert.Equal(DateTimeOffset.Parse("2026-07-01T12:00:00Z"), notice.ReceivedAt);
    Assert.Equal("Original photograph.", notice.WorkDescription);
    Assert.Equal(notice.ReceivedAt, notice.WaitingSince);
    Assert.Equal(["deadline_due", "form_intake_review"], notice.Reasons);
    Assert.Empty(notice.Targets);
    Assert.Empty(notice.Evidence);
    Assert.Empty(notice.Restrictions);
    Assert.Empty(notice.Appeals);
    Assert.Empty(notice.CounterNotices);
    Assert.Empty(notice.LegalHolds);
    Assert.Empty(notice.DeliveryIntents);
    Assert.Empty(notice.EmailCorrespondence);
    Assert.Equal("Claimant", notice.Claimant.DisplayName);
    Assert.Equal("claimant@example.test", notice.Claimant.Contact);
    Assert.Equal("00000000-0000-7000-8000-000000000807", notice.Claimant.UserId);
    var misuse = Assert.IsType<CopyrightClaimantMisuseSummary>(notice.Claimant.Misuse);
    Assert.Equal(4, misuse.NoticeCount);
    Assert.Equal(2, misuse.NoticeRejected);
    Assert.Equal(1, misuse.NoticeWithdrawn);
    Assert.Equal(1, misuse.RestrictionReversedByAppeal);
    Assert.Equal(0, misuse.RestrictionReversedByCounterNotice);
    Assert.Equal(DateTimeOffset.Parse("2026-06-01T12:00:00Z"), misuse.OldestNoticeAt);
    Assert.Equal(notice.ReceivedAt, misuse.NewestNoticeAt);
    Assert.False(misuse.PageInfo.HasNextPage);
    var review = Assert.IsType<CopyrightStaffFormReview>(notice.FormReview);
    Assert.Equal("00000000-0000-7000-8000-000000000806", review.IntakeId);
    Assert.Equal("guest_form", review.SourceKind);
    Assert.Null(review.Review);
    var screening = Assert.IsType<CopyrightStaffFormScreening>(review.Screening);
    Assert.Equal("completed", screening.State);
    Assert.Equal("not_obviously_invalid", screening.Recommendation);
    Assert.Equal("No obvious spam markers.", screening.Rationale);
    var guidance = Assert.IsType<CopyrightStaffFormGuidance>(screening.Guidance);
    Assert.Equal("A claimant reports an unlicensed copy of an original photograph.", guidance.Summary);
    Assert.Equal("request_information", guidance.SuggestedAction);
    var missingDetail = Assert.Single(guidance.Elements, element => element.Element == "material_identification");
    Assert.Equal("unclear", missingDetail.Status);
    Assert.Equal("The hosted use URL does not name the specific image.", missingDetail.Gap);
    Assert.Null(guidance.Elements[0].Gap);
    var risk = Assert.Single(guidance.RiskNotes);
    Assert.Equal("possible_fair_use", risk.Kind);
    Assert.Equal("The post may be a critical review.", risk.Note);
    var deadline = Assert.IsType<CopyrightStaffDeadline>(notice.NextDeadline);
    Assert.Equal(DateTimeOffset.Parse("2026-07-14T12:00:00Z"), deadline.EscalationAt);
    Assert.Equal(DateTimeOffset.Parse("2026-07-15T12:00:00Z"), deadline.RestorationDeadlineAt);
  }

  [Fact]
  public void CopyrightQueueDistinguishesFailedActionsAndExactFromPerceptualMatches()
  {
    var notice = Fixture<CopyrightStaffQueueResponse>("web.copyright.staff-queue.default").CopyrightNotices[1];
    var review = Assert.IsType<CopyrightStaffIntakeReview>(notice.FormReview?.Review);
    Assert.True(review.IsAccepted);
    Assert.Equal(DateTimeOffset.Parse("2026-07-01T11:30:00Z"), review.ReviewedAt);
    Assert.Equal("00000000-0000-7000-8000-000000000817", review.ReviewedById);
    var target = Assert.Single(notice.Targets);
    Assert.Equal("00000000-0000-7000-8000-000000000805", target.Id);
    Assert.Equal("image-placement:00000000-0000-7000-8000-000000000826", target.PlacementKey);
    Assert.Equal(1, target.PlacementRevision);
    Assert.Equal("00000000-0000-7000-8000-000000000820", target.ImageId);
    Assert.Equal("post-image", target.Surface);
    Assert.Null(target.Provenance);
    Assert.Equal(new Uri("https://voucha.ai/discussion/original-photograph"), target.HostedUseUrl);
    var action = Assert.Single(notice.ActionIntents);
    Assert.Equal("00000000-0000-7000-8000-000000000825", action.Id);
    Assert.Equal(target.Id, action.TargetId);
    Assert.Equal("withhold", action.Action);
    Assert.Equal("failed", action.State);
    Assert.Equal("Storage unavailable", action.FailureMessage);
    Assert.False(notice.StaydownMatchesHasMore);
    var match = notice.StaydownMatches[0];
    Assert.Equal("00000000-0000-7000-8000-000000000818", match.Id);
    Assert.Equal("00000000-0000-7000-8000-000000000819", match.ImageId);
    Assert.Equal(target.ImageId, match.RegisteredImageId);
    Assert.Equal(target.Id, match.TargetId);
    Assert.Equal("00000000-0000-7000-8000-000000000821", match.UploadedById);
    Assert.Equal("photo-author", match.UploadedByUsername);
    Assert.Equal("perceptual", match.MatchKind);
    Assert.Equal(3, match.HammingDistance);
    Assert.Equal(DateTimeOffset.Parse("2026-07-01T11:45:00Z"), match.MatchedAt);
    var placement = Assert.IsType<CopyrightStaffImagePlacement>(match.ImagePlacement);
    Assert.Equal(match.ImageId, placement.ImageId);
    Assert.Equal("00000000-0000-7000-8000-000000000822", placement.PlacementId);
    Assert.Equal(1, placement.PlacementRevision);
    var exact = notice.StaydownMatches[1];
    Assert.Equal("exact", exact.MatchKind);
    Assert.Equal(0, exact.HammingDistance);
    Assert.Null(exact.ImagePlacement);
    Assert.Null(exact.UploadedByUsername);
  }
}
