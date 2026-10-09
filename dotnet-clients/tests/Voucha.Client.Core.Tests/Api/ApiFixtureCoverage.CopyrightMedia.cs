using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

internal static partial class ApiFixtureCoverage
{
  public static Dictionary<string, Type> WithCopyrightMediaFixtures(
      this Dictionary<string, Type> registry)
  {
    registry["native.posts.images.placement.default"] = typeof(PostImagePlacementResponse);
    registry["native.moderation.copyright.image-similarity-candidates.default"] =
        typeof(CopyrightImageSimilarityCandidatesResponse);
    registry["web.copyright.notices.default"] = typeof(CopyrightNoticesResponse);
    registry["web.copyright.notices.null-claimant"] = typeof(CopyrightNoticesResponse);
    registry["web.copyright.notice.detail.populated"] = typeof(CopyrightNoticeResponse);
    registry["web.copyright.notice.detail.null-claimant"] = typeof(CopyrightNoticeResponse);
    registry["web.copyright.notice.participant.populated"] = typeof(CopyrightParticipantNoticeResponse);
    registry["web.copyright.notice.participant.null-claimant"] = typeof(CopyrightParticipantNoticeResponse);
    registry["web.copyright.eu.participant.no-action-complaint"] = typeof(CopyrightParticipantNoticeResponse);
    registry["web.copyright.eu.dispute-settlements.participant"] = typeof(CopyrightEuDisputeSettlementsResponse);
    return registry;
  }

  public static Dictionary<string, ApiRequest> WithCopyrightMediaEndpoints(
      this Dictionary<string, ApiRequest> registry)
  {
    registry["native.posts.images.placement.default"] = VouchaApiEndpoints.PostImages(
        ApiFixtureLoader.RouteParameterValue("native.posts.images.placement.default", "idOrSlug"));
    registry["native.moderation.copyright.image-similarity-candidates.default"] =
        VouchaApiEndpoints.CopyrightImageSimilarityCandidates(
            ApiFixtureLoader.RouteParameterValue(
                "native.moderation.copyright.image-similarity-candidates.default", "id"),
            ApiFixtureLoader.RouteParameterValue(
                "native.moderation.copyright.image-similarity-candidates.default", "targetId"));
    registry["web.copyright.notices.default"] = VouchaApiEndpoints.CopyrightNotices();
    registry["web.copyright.notices.null-claimant"] = VouchaApiEndpoints.CopyrightNotices();
    registry["web.copyright.notice.detail.populated"] = VouchaApiEndpoints.CopyrightNotice(ApiFixtureLoader.RouteParameterValue("web.copyright.notice.detail.populated", "id"));
    registry["web.copyright.notice.detail.null-claimant"] = VouchaApiEndpoints.CopyrightNotice(ApiFixtureLoader.RouteParameterValue("web.copyright.notice.detail.null-claimant", "id"));
    registry["web.copyright.notice.participant.populated"] = VouchaApiEndpoints.CopyrightParticipantNotice(ApiFixtureLoader.RouteParameterValue("web.copyright.notice.participant.populated", "id"));
    registry["web.copyright.notice.participant.null-claimant"] = VouchaApiEndpoints.CopyrightParticipantNotice(ApiFixtureLoader.RouteParameterValue("web.copyright.notice.participant.null-claimant", "id"));
    registry["web.copyright.eu.participant.no-action-complaint"] = VouchaApiEndpoints.CopyrightParticipantNotice(
        ApiFixtureLoader.RouteParameterValue("web.copyright.eu.participant.no-action-complaint", "id"));
    registry["web.copyright.eu.dispute-settlements.participant"] = VouchaApiEndpoints.CopyrightEuDisputeSettlements(
        ApiFixtureLoader.RouteParameterValue("web.copyright.eu.dispute-settlements.participant", "id"));
    return registry;
  }
}
