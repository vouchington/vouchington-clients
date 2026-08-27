import VouchaModels

let moderationParityFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.moderation.removed-posts.default") {
        try assertFixtureCoversDTO($0, as: MemberRemovedPostNoticesResponse.self)
    },
    RegisteredFixture(id: "native.moderation.removed-posts.page-2") {
        try assertFixtureCoversDTO($0, as: MemberRemovedPostNoticesResponse.self)
    },
    RegisteredFixture(id: "native.moderation.disputes.detail.default") {
        try assertFixtureCoversDTO(
            $0,
            as: ReviewDisputeResponse.self,
            ignoring: reviewDisputeEnvelopeNilFieldIgnores
        )
    },
    RegisteredFixture(id: "native.moderation.disputes.update.default") {
        try assertFixtureCoversDTO(
            $0,
            as: ReviewDisputeResponse.self,
            ignoring: reviewDisputeEnvelopeNilFieldIgnores
        )
    },
    RegisteredFixture(id: "native.moderation.disputes.approval.default") {
        try assertFixtureCoversDTO(
            $0,
            as: ReviewDisputeResponse.self,
            ignoring: reviewDisputeEnvelopeNilFieldIgnores
        )
    },
    RegisteredFixture(id: "native.moderation.disputes.delivery.default") {
        try assertFixtureCoversDTO(
            $0,
            as: ReviewDisputeResponse.self,
            ignoring: reviewDisputeEnvelopeNilFieldIgnores
        )
    },
    RegisteredFixture(id: "native.moderation.disputes.resolution.remove") {
        try assertFixtureCoversDTO(
            $0,
            as: ReviewDisputeResponse.self,
            ignoring: reviewDisputeEnvelopeNilFieldIgnores
        )
    },
    RegisteredFixture(id: "native.moderation.disputes.resolution.annotate") {
        try assertFixtureCoversDTO(
            $0,
            as: ReviewDisputeResponse.self,
            ignoring: reviewDisputeEnvelopeNilFieldIgnores
        )
    },
    RegisteredFixture(id: "native.moderation.disputes.resolution.dismiss") {
        try assertFixtureCoversDTO(
            $0,
            as: ReviewDisputeResponse.self,
            ignoring: reviewDisputeEnvelopeNilFieldIgnores
        )
    },
    RegisteredFixture(id: "native.moderation.disputes.resolution-drafts.default") {
        try assertFixtureCoversDTO($0, as: ReviewDisputeQueueResponse.self)
    },
    RegisteredFixture(id: "native.moderation.exposure.default") {
        try assertFixtureCoversDTO($0, as: ModerationExposureResponse.self)
    },
    RegisteredFixture(id: "native.moderation.reveals.default") {
        try assertFixtureCoversDTO($0, as: ModerationExposureResponse.self)
    }
]

private let reviewDisputeEnvelopeNilFieldIgnores: Set<String> = [
    "dispute.ai_drafted_at",
    "dispute.ai_internal_response",
    "dispute.ai_public_response",
    "dispute.approved_at",
    "dispute.approved_by_id",
    "dispute.drafted_at",
    "dispute.edited_at",
    "dispute.edited_by_id",
    "dispute.internal_notes",
    "dispute.latest_lifecycle_change_id",
    "dispute.model",
    "dispute.public_response",
    "dispute.resolution_action",
    "dispute.resolved_at",
    "dispute.resolved_by_id",
    "dispute.sent_at"
]
