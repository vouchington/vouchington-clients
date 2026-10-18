using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

internal static partial class ApiFixtureCoverage
{
  private static Dictionary<string, ApiRequest> WithModerationParityEndpoints(
      this Dictionary<string, ApiRequest> registry)
  {
    registry["native.moderation.appeals.member.default"] =
        VouchaApiEndpoints.Appeals(ModerationAppealStatus.Pending, mine: true);
    registry["native.moderation.appeals.member.page-2"] = VouchaApiEndpoints.Appeals(
        ModerationAppealStatus.Pending,
        after: ApiFixtureLoader.QueryValue("native.moderation.appeals.member.page-2", "after"), mine: true);
    registry["native.moderation.appeals.member.resolved"] =
        VouchaApiEndpoints.Appeals(ModerationAppealStatus.Resolved, mine: true);
    registry["native.moderation.appeals.detail.member.default"] =
        VouchaApiEndpoints.Appeal(Id("native.moderation.appeals.detail.member.default"));
    registry["native.moderation.appeals.detail.member.sent"] =
        VouchaApiEndpoints.Appeal(Id("native.moderation.appeals.detail.member.sent"));
    registry["native.moderation.appeals.create.member.default"] = VouchaApiEndpoints.SubmitAppeal(
        new ModerationAppealSubmissionRequest(
            ModerationAppealTargetType.Suspension, null, ModerationAppealReason.Other,
            "Please review this again."));
    registry["native.moderation.removed-posts.default"] =
        VouchaApiEndpoints.PersonalRemovedPosts();
    registry["native.moderation.removed-posts.page-2"] =
        VouchaApiEndpoints.PersonalRemovedPosts(
            after: ApiFixtureLoader.QueryValue(
                "native.moderation.removed-posts.page-2",
                "after"));
    registry["native.moderation.disputes.detail.default"] = VouchaApiEndpoints.Dispute(
        Id("native.moderation.disputes.detail.default"));
    registry["native.moderation.disputes.member.default"] = VouchaApiEndpoints.Disputes("pending");
    registry["native.moderation.disputes.detail.member.default"] = VouchaApiEndpoints.Dispute(
        Id("native.moderation.disputes.detail.member.default"));
    registry["native.moderation.disputes.update.default"] = VouchaApiEndpoints.UpdateDispute(
        Id("native.moderation.disputes.update.default"),
        "We reviewed your dispute.",
        "Reviewed against the content policy.");
    registry["native.moderation.disputes.approval.default"] = VouchaApiEndpoints.DisputeApproval(
        Id("native.moderation.disputes.approval.default"));
    registry["native.moderation.disputes.delivery.default"] = VouchaApiEndpoints.DisputeDelivery(
        Id("native.moderation.disputes.delivery.default"));
    registry["native.moderation.disputes.resolution.remove"] = VouchaApiEndpoints.DisputeResolution(
        Id("native.moderation.disputes.resolution.remove"),
        ModerationDisputeResolutionAction.Remove);
    registry["native.moderation.disputes.resolution.annotate"] = VouchaApiEndpoints.DisputeResolution(
        Id("native.moderation.disputes.resolution.annotate"),
        ModerationDisputeResolutionAction.Annotate,
        "This review reflects a disputed experience.");
    registry["native.moderation.disputes.resolution.dismiss"] = VouchaApiEndpoints.DisputeResolution(
        Id("native.moderation.disputes.resolution.dismiss"),
        ModerationDisputeResolutionAction.Dismiss);
    registry["native.moderation.disputes.resolution-drafts.default"] =
        VouchaApiEndpoints.DisputeResolutionDrafts(
            Id("native.moderation.disputes.resolution-drafts.default"));
    registry["native.moderation.exposure.default"] = VouchaApiEndpoints.ModerationExposure();
    registry["native.moderation.reveals.default"] = VouchaApiEndpoints.RecordModerationReveal(
        "00000000-0000-7000-8000-000000000301",
        null,
        ModerationRevealSurface.ReviewQueue);
    return registry;
  }

  private static string Id(string fixtureId) =>
      ApiFixtureLoader.RouteParameterValue(fixtureId, "id");
}
