@testable import VouchaAPI
import VouchaModels

extension ApiFixtureEndpointCoverageTests {
    static let moderationParityEndpointRegistry: [String: Endpoint] = [
        "native.moderation.removed-posts.default": .memberRemovedPostNotices(limit: 25),
        "native.moderation.removed-posts.page-2": .memberRemovedPostNotices(
            after: removedPostsPageTwoCursor,
            limit: 25
        ),
        "native.moderation.disputes.detail.default": .dispute(id: moderationDisputeFixtureId),
        "native.moderation.disputes.update.default": .updateDisputeDraft(
            id: moderationDisputeFixtureId,
            publicResponse: "We reviewed your dispute.",
            internalNotes: "Reviewed against the content policy."
        ),
        "native.moderation.disputes.approval.default": .disputeApproval(
            id: moderationDisputeFixtureId
        ),
        "native.moderation.disputes.delivery.default": .disputeDelivery(
            id: moderationDisputeFixtureId
        ),
        "native.moderation.disputes.resolution.remove": .disputeResolution(
            id: moderationDisputeFixtureId,
            action: .remove
        ),
        "native.moderation.disputes.resolution.annotate": .disputeResolution(
            id: moderationDisputeFixtureId,
            action: .annotate,
            bodyText: "This review reflects a disputed experience."
        ),
        "native.moderation.disputes.resolution.dismiss": .disputeResolution(
            id: moderationDisputeFixtureId,
            action: .dismiss
        ),
        "native.moderation.disputes.resolution-drafts.default": .disputeResolutionDrafts(
            id: moderationDisputeFixtureId
        ),
        "native.moderation.exposure.default": .moderationExposure(),
        "native.moderation.reveals.default": .recordModerationReveal(
            postId: "00000000-0000-7000-8000-000000000301",
            surface: .reviewQueue
        )
    ]
}

let moderationDisputeFixtureId = "00000000-0000-7000-8000-000000000201"
private let removedPostsPageTwoCursor =
    "eyJ0aW1lc3RhbXAiOiIyMDI2LTA3LTAxVDA5OjAwOjAwLjAwMDAwMFoiLCJ0aWVyIjowLCJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDIwMSIsInNjb3BlIjoidXNlci1yZW1vdmVkLXBvc3RzOjAwMDAwMDAwLTAwMDAtNzAwMC04NzAwLTAwMDAwMDAwMDEwMTpyZW1vdmVkLWRlc2Mta2luZC1kZXNjLXBvc3QtZGVzYzp2MiJ9"
