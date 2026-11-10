using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class ApiFixtureEndpointCoverageTests
{
  private static IReadOnlyDictionary<string, ApiRequest> CreateModerationParityRegistry() =>
      new Dictionary<string, ApiRequest>(StringComparer.Ordinal)
      {
        ["native.moderation.context.member"] = VouchaApiEndpoints.UserModerationContext(
            ApiFixtureLoader.RouteParameterValue("native.moderation.context.member", "userId")),
        ["native.moderation.context.staff"] = VouchaApiEndpoints.UserModerationContext(
            ApiFixtureLoader.RouteParameterValue("native.moderation.context.staff", "userId")),
        ["native.moderation.transparency.default"] =
            VouchaApiEndpoints.ModerationTransparency(),
        ["native.community.moderation-transparency.default"] =
            VouchaApiEndpoints.CommunityModerationTransparency("fixture-community"),
        ["native.moderation.removed-posts.default"] =
            VouchaApiEndpoints.PersonalRemovedPosts(),
        ["native.moderation.removed-posts.page-2"] =
            VouchaApiEndpoints.PersonalRemovedPosts(
                after: ApiFixtureLoader.QueryValue(
                    "native.moderation.removed-posts.page-2",
                    "after")),
        ["native.moderation.disputes.detail.default"] = VouchaApiEndpoints.Dispute(
            Id("native.moderation.disputes.detail.default")),
        ["native.moderation.disputes.update.default"] = VouchaApiEndpoints.UpdateDispute(
            Id("native.moderation.disputes.update.default"),
            "We reviewed your dispute.",
            "Reviewed against the content policy."),
        ["native.moderation.disputes.approval.default"] = VouchaApiEndpoints.DisputeApproval(
            Id("native.moderation.disputes.approval.default")),
        ["native.moderation.disputes.delivery.default"] = VouchaApiEndpoints.DisputeDelivery(
            Id("native.moderation.disputes.delivery.default")),
        ["native.moderation.disputes.resolution.remove"] = VouchaApiEndpoints.DisputeResolution(
            Id("native.moderation.disputes.resolution.remove"),
            ModerationDisputeResolutionAction.Remove),
        ["native.moderation.disputes.resolution.annotate"] = VouchaApiEndpoints.DisputeResolution(
            Id("native.moderation.disputes.resolution.annotate"),
            ModerationDisputeResolutionAction.Annotate,
            "This review reflects a disputed experience."),
        ["native.moderation.disputes.resolution.dismiss"] = VouchaApiEndpoints.DisputeResolution(
            Id("native.moderation.disputes.resolution.dismiss"),
            ModerationDisputeResolutionAction.Dismiss),
        ["native.moderation.disputes.resolution-drafts.default"] =
            VouchaApiEndpoints.DisputeResolutionDrafts(
                Id("native.moderation.disputes.resolution-drafts.default")),
        ["native.moderation.report-detail.pending"] = VouchaApiEndpoints.ModerationReport(Id("native.moderation.report-detail.pending")),
        ["native.moderation.report-detail.saved"] = VouchaApiEndpoints.ModerationReport(Id("native.moderation.report-detail.saved")),
        ["native.moderation.report-judgement-run.succeeded"] = VouchaApiEndpoints.ModerationReportJudgementRun(
            Id("native.moderation.report-judgement-run.succeeded"), RunId("native.moderation.report-judgement-run.succeeded")),
        ["native.moderation.report-judgement-run.failed"] = VouchaApiEndpoints.ModerationReportJudgementRun(
            Id("native.moderation.report-judgement-run.failed"), RunId("native.moderation.report-judgement-run.failed")),
        ["native.moderation.report-judgement-run.expired"] = VouchaApiEndpoints.ModerationReportJudgementRun(
            Id("native.moderation.report-judgement-run.expired"), RunId("native.moderation.report-judgement-run.expired")),
        ["native.moderation.appeals.resolution-draft-run.active"] = VouchaApiEndpoints.AppealResolutionDraftRun(
            Id("native.moderation.appeals.resolution-draft-run.active"), RunId("native.moderation.appeals.resolution-draft-run.active")),
        ["native.moderation.appeals.resolution-draft-run.succeeded"] = VouchaApiEndpoints.AppealResolutionDraftRun(
            Id("native.moderation.appeals.resolution-draft-run.succeeded"), RunId("native.moderation.appeals.resolution-draft-run.succeeded")),
        ["native.moderation.appeals.resolution-draft-run.failed"] = VouchaApiEndpoints.AppealResolutionDraftRun(
            Id("native.moderation.appeals.resolution-draft-run.failed"), RunId("native.moderation.appeals.resolution-draft-run.failed")),
        ["native.moderation.appeals.resolution-draft-run.expired"] = VouchaApiEndpoints.AppealResolutionDraftRun(
            Id("native.moderation.appeals.resolution-draft-run.expired"), RunId("native.moderation.appeals.resolution-draft-run.expired")),
        ["native.moderation.exposure.default"] = VouchaApiEndpoints.ModerationExposure(),
        ["native.moderation.reveals.default"] = VouchaApiEndpoints.RecordModerationReveal(
            "00000000-0000-7000-8000-000000000301",
            null,
            ModerationRevealSurface.ReviewQueue),
      };

  private static string Id(string fixtureId) =>
      ApiFixtureLoader.RouteParameterValue(fixtureId, "id");

  private static string RunId(string fixtureId) =>
      ApiFixtureLoader.RouteParameterValue(fixtureId, "runId");
}
