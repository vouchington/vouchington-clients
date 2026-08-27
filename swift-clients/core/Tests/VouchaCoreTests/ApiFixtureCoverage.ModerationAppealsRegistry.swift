import VouchaModels

let moderationAppealLifecycleFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.moderation.appeals.page-2") {
        try assertFixtureCoversDTO(
            $0,
            as: ModerationAppealListResponse.self,
            ignoring: [
                "appeals.approved_at",
                "appeals.approved_by_id",
                "appeals.community_ban_id",
                "appeals.community_id",
                "appeals.drafted_at",
                "appeals.edited_at",
                "appeals.edited_by_id",
                "appeals.internal_notes",
                "appeals.latest_lifecycle_change_id",
                "appeals.post_id",
                "appeals.post_removal_kind",
                "appeals.public_response",
                "appeals.resolution_action",
                "appeals.resolved_at",
                "appeals.resolved_by_id",
                "appeals.sent_at"
            ]
        )
    },
    RegisteredFixture(id: "native.moderation.appeals.update.default") {
        try assertFixtureCoversDTO(
            $0,
            as: ModerationAppealEnvelope.self,
            ignoring: [
                "appeal.approved_at",
                "appeal.approved_by_id",
                "appeal.community_ban_id",
                "appeal.community_id",
                "appeal.internal_notes",
                "appeal.resolution_action",
                "appeal.resolved_at",
                "appeal.resolved_by_id",
                "appeal.sent_at",
                "appeal.user_warning_id"
            ]
        )
    },
    RegisteredFixture(id: "native.moderation.appeals.approval.default") {
        try assertFixtureCoversDTO(
            $0,
            as: ModerationAppealEnvelope.self,
            ignoring: [
                "appeal.community_ban_id",
                "appeal.community_id",
                "appeal.internal_notes",
                "appeal.resolution_action",
                "appeal.resolved_at",
                "appeal.resolved_by_id",
                "appeal.sent_at",
                "appeal.user_warning_id"
            ]
        )
    },
    RegisteredFixture(id: "native.moderation.appeals.delivery.default") {
        try assertFixtureCoversDTO(
            $0,
            as: ModerationAppealEnvelope.self,
            ignoring: [
                "appeal.community_ban_id",
                "appeal.community_id",
                "appeal.internal_notes",
                "appeal.resolution_action",
                "appeal.resolved_at",
                "appeal.resolved_by_id",
                "appeal.user_warning_id"
            ]
        )
    },
    RegisteredFixture(id: "native.moderation.appeals.resolution.accept") {
        try assertFixtureCoversDTO(
            $0,
            as: ModerationAppealEnvelope.self,
            ignoring: moderationAppealResolvedNilFieldIgnores
        )
    },
    RegisteredFixture(id: "native.moderation.appeals.resolution.reduce") {
        try assertFixtureCoversDTO(
            $0,
            as: ModerationAppealEnvelope.self,
            ignoring: moderationAppealResolvedNilFieldIgnores
        )
    },
    RegisteredFixture(id: "native.moderation.appeals.resolution.deny") {
        try assertFixtureCoversDTO(
            $0,
            as: ModerationAppealEnvelope.self,
            ignoring: moderationAppealResolvedNilFieldIgnores
        )
    },
    RegisteredFixture(id: "native.moderation.appeals.resolution-drafts.default") {
        try assertFixtureCoversDTO($0, as: ModerationAppealQueueResponse.self)
    }
]

private let moderationAppealResolvedNilFieldIgnores: Set<String> = [
    "appeal.community_ban_id",
    "appeal.community_id",
    "appeal.internal_notes",
    "appeal.user_warning_id"
]
