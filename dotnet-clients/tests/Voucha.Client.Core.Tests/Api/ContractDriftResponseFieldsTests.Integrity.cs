using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class ContractDriftResponseFieldsTests
{
  [Fact]
  public void SavedListItemsDistinguishKnownAndUnavailableEntityLabels()
  {
    var items = Fixture<ListItemsResponse>("native.list-items.default").ListItems;
    Assert.Equal("Saved article", items["list-item-1"].EntityLabel);
    Assert.Null(items["list-item-2"].EntityLabel);
    var clusters = Fixture<StaffClusteredModerationReportsResponse>("native.moderation.reports.clustered.default");
    var context = Assert.Single(clusters.Clusters.SelectMany(cluster => cluster.Reports),
        report => report.CommunityBanEvasion is not null).CommunityBanEvasion;
    Assert.NotNull(context);
    Assert.Equal("previously-banned", context.SourceUsername);
    Assert.Null(context.SourceDisplayName);
    Assert.Equal(0.93, context.Score);
  }

  [Fact]
  public void IntegrityFlagsRetainEntityLabelsWhileResolverProfilesMayBeMissing()
  {
    var vote = Assert.Single(Fixture<VoteIntegrityFlagsResponse>("native.moderation.vote-integrity.resolved").Results);
    Assert.Equal("Reported post", vote.EntityLabel);
    Assert.Null(vote.ResolverUsername);
    Assert.Null(vote.ResolverDisplayName);
    var report = Assert.Single(Fixture<ReportIntegrityFlagsResponse>("native.moderation.report-integrity.resolved").Results);
    Assert.Equal("Reported post", report.EntityLabel);
    Assert.Null(report.ReportedUsername);
    Assert.Null(report.ResolverUsername);
    Assert.Null(report.ResolverDisplayName);
    Assert.Equal("dismissed", report.Resolution);
    Assert.Equal("admin-1", report.ResolvedById);
  }

  [Fact]
  public void VotePenaltyPreservesRecipientCreatorAndScheduledReview()
  {
    var penalty = Fixture<VoteWeightPenaltyResponse>("native.moderation.vote-integrity.penalties.get").Penalty;
    Assert.Equal("penalty-recipient", penalty.UserUsername);
    Assert.Null(penalty.UserDisplayName);
    Assert.Equal("moderator", penalty.CreatorUsername);
    Assert.Null(penalty.CreatorDisplayName);
    Assert.Null(penalty.RevokerUsername);
    Assert.Null(penalty.RevokerDisplayName);
    Assert.Equal("voting_ring", penalty.SourceFlagType);
    Assert.Null(penalty.ReviewedById);
    Assert.Null(penalty.ReviewedAt);
    Assert.Equal(DateTimeOffset.Parse("2026-10-01T12:00:00Z"), penalty.ReviewDueAt);
    Assert.Equal(0.2, penalty.PenaltyMultiplier);
  }

  [Fact]
  public void RevokedReportPenaltyRetainsRecipientAndMutationIdentifiers()
  {
    var response = Fixture<ReportAbusePenaltyResponse>("native.moderation.report-integrity.penalties.revoke");
    var penalty = response.Penalty;
    Assert.Equal(penalty.Id, response.PenaltyId);
    Assert.Equal(penalty.UserId, response.UserId);
    Assert.Equal("penalty-recipient", penalty.UserUsername);
    Assert.Null(penalty.UserDisplayName);
    Assert.Equal("moderator", penalty.CreatorUsername);
    Assert.Null(penalty.CreatorDisplayName);
    Assert.Null(penalty.RevokerUsername);
    Assert.Null(penalty.RevokerDisplayName);
    Assert.Equal("mass_report_suspected", penalty.SourceFlagType);
    Assert.Null(penalty.ReviewedById);
    Assert.Null(penalty.ReviewedAt);
    Assert.Equal(DateTimeOffset.Parse("2026-10-01T12:00:00Z"), penalty.ReviewDueAt);
    Assert.Equal(DateTimeOffset.Parse("2026-06-02T12:00:00Z"), penalty.RevokedAt);
    Assert.Equal(penalty.RevokedAt, penalty.UpdatedAt);
  }

  [Fact]
  public void ReportResolutionRetainsResolverWithoutInventingDeletedProfileNames()
  {
    var report = Fixture<ModerationReportResolutionResponse>("native.moderation.report-resolution.reviewed").Report;
    Assert.Equal("moderator", report.ResolverUsername);
    Assert.Null(report.ResolverDisplayName);
    Assert.Null(report.ReporterDisplayName);
    Assert.Equal(ModerationReportResolution.Reviewed, report.Status);
    Assert.Equal("admin-1", report.ResolvedById);
  }

  [Fact]
  public void QueueCandidatesAndCommunityApplicationsPreserveDisplayAndPaginationFields()
  {
    var queue = Fixture<AdminReviewQueueResponse>("native.moderation.review-queue.default");
    Assert.Equal("Comment in review", queue.Results[0].PostLabel);
    Assert.Equal("Rejected review post", queue.Results[1].PostLabel);
    var candidates = Fixture<LandingPageCandidatesResponse>("native.landing-page-candidates.default");
    Assert.Equal(100, candidates.ItemLimit);
    Assert.Equal("opaque-landing-candidates-start", candidates.PageInfo?.StartCursor);
    Assert.False(candidates.PageInfo?.HasNextPage);
    var applications = Fixture<CommunityApplicationsResponse>("web.communities.applications.default");
    var summary = applications.CommunityApplicationSummaries["application-1"];
    Assert.Equal("community_application_summary", summary.EntityType);
    Assert.Equal("I would like to join.", summary.MessagePreview);
    var claim = Assert.Single(Fixture<TopicClaimPage>("native.my-topic-claims.page").Results);
    Assert.Equal("claimant", claim.ClaimantUsername);
    Assert.Null(claim.ClaimantDisplayName);
  }
}
