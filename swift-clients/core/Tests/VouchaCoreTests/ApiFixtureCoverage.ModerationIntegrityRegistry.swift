import VouchaModels

let nativeModerationIntegrityCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.moderation.vote-integrity.default") {
        try assertFixtureCoversDTO(
            $0,
            as: VoteIntegrityFlagsResponse.self,
            ignoring: [
                "page_info.end_cursor",
                "results.agent_moderation_id",
                "results.entity_relation_id",
                "results.hostname_id",
                "results.resolution",
                "results.resolved_at",
                "results.resolved_by_id",
                "results.rss_feed_item_id",
                "results.topic_id"
            ]
        )
    },
    RegisteredFixture(id: "native.moderation.report-integrity.default") {
        try assertFixtureCoversDTO(
            $0,
            as: ReportIntegrityFlagsResponse.self,
            ignoring: [
                "page_info.end_cursor",
                "results.hostname_id",
                "results.reported_user_id",
                "results.resolution",
                "results.resolved_at",
                "results.resolved_by_id",
                "results.rss_feed_item_id"
            ]
        )
    },
    RegisteredFixture(id: "native.moderation.vote-integrity.pending") {
        try assertFixtureCoversDTO(
            $0,
            as: VoteIntegrityFlagsResponse.self,
            ignoring: [
                "results.agent_moderation_id",
                "results.entity_relation_id",
                "results.hostname_id",
                "results.resolution",
                "results.resolved_at",
                "results.resolved_by_id",
                "results.rss_feed_item_id",
                "results.topic_id"
            ]
        )
    },
    RegisteredFixture(id: "native.moderation.vote-integrity.resolved") {
        try assertFixtureCoversDTO(
            $0,
            as: VoteIntegrityFlagsResponse.self,
            ignoring: [
                "results.agent_moderation_id",
                "results.entity_relation_id",
                "results.hostname_id",
                "results.post_id",
                "results.rss_feed_item_id"
            ]
        )
    },
    RegisteredFixture(id: "native.moderation.vote-integrity.resolution.dismissed") {
        try assertFixtureCoversDTO(
            $0,
            as: VoteIntegrityFlagEnvelope.self,
            ignoring: voteIntegrityEnvelopeNullTargets
        )
    },
    RegisteredFixture(id: "native.moderation.vote-integrity.resolution.penalized") {
        try assertFixtureCoversDTO(
            $0,
            as: VoteIntegrityFlagEnvelope.self,
            ignoring: voteIntegrityEnvelopeNullTargets
        )
    },
    RegisteredFixture(id: "native.moderation.vote-integrity.resolution.suspended") {
        try assertFixtureCoversDTO(
            $0,
            as: VoteIntegrityFlagEnvelope.self,
            ignoring: voteIntegrityEnvelopeNullTargets
        )
    },
    RegisteredFixture(id: "native.moderation.vote-integrity.penalty") {
        try assertFixtureCoversDTO($0, as: VoteIntegrityPenaltyResponse.self)
    },
    RegisteredFixture(id: "native.moderation.report-integrity.pending") {
        try assertFixtureCoversDTO(
            $0,
            as: ReportIntegrityFlagsResponse.self,
            ignoring: [
                "results.hostname_id",
                "results.reported_user_id",
                "results.resolution",
                "results.resolved_at",
                "results.resolved_by_id",
                "results.rss_feed_item_id"
            ]
        )
    },
    RegisteredFixture(id: "native.moderation.report-integrity.resolved") {
        try assertFixtureCoversDTO(
            $0,
            as: ReportIntegrityFlagsResponse.self,
            ignoring: ["results.hostname_id", "results.post_id", "results.rss_feed_item_id"]
        )
    },
    RegisteredFixture(id: "native.moderation.report-integrity.resolution.dismissed") {
        try assertFixtureCoversDTO(
            $0,
            as: ReportIntegrityFlagEnvelope.self,
            ignoring: reportIntegrityEnvelopeNullTargets
        )
    },
    RegisteredFixture(id: "native.moderation.report-integrity.penalty") {
        try assertFixtureCoversDTO(
            $0,
            as: ReportIntegrityPenaltyResponse.self,
            ignoring: reportIntegrityEnvelopeNullTargets
        )
    }
] + nativeIntegrityPenaltyCoverage

private let nativeIntegrityPenaltyCoverage: [RegisteredFixture] =
    ["default", "active", "revoked", "all", "page-2"].map { suffix in
        RegisteredFixture(id: "native.moderation.report-integrity.penalties.\(suffix)") {
            try assertFixtureCoversDTO(
                $0,
                as: ReportIntegrityPenaltiesResponse.self,
                ignoring: [
                    "page_info.end_cursor",
                    "results.created_by_id",
                    "results.revoked_at",
                    "results.revoked_by_id"
                ]
            )
        }
    } + [
        RegisteredFixture(id: "native.moderation.report-integrity.penalties.get") {
            try assertFixtureCoversDTO(
                $0,
                as: ReportIntegrityPenaltyEnvelope.self,
                ignoring: ["penalty.revoked_at", "penalty.revoked_by_id"]
            )
        },
        RegisteredFixture(id: "native.moderation.report-integrity.penalties.revoke") {
            try assertFixtureCoversDTO(
                $0,
                as: ReportIntegrityPenaltyRevokeResponse.self,
                ignoring: ["penaltyId", "userId"]
            )
        }
    ] + ["default", "active", "revoked", "all", "page-2"].map { suffix in
        RegisteredFixture(id: "native.moderation.vote-integrity.penalties.\(suffix)") {
            try assertFixtureCoversDTO(
                $0,
                as: VoteIntegrityPenaltiesResponse.self,
                ignoring: [
                    "page_info.end_cursor",
                    "filter_scope.source_flag_id",
                    "results.created_by_id",
                    "results.revoked_at",
                    "results.revoked_by_id",
                    "results.source_flag_id"
                ]
            )
        }
    } + [
        RegisteredFixture(id: "native.moderation.vote-integrity.penalties.get") {
            try assertFixtureCoversDTO(
                $0,
                as: VoteIntegrityPenaltyEnvelope.self,
                ignoring: ["penalty.revoked_at", "penalty.revoked_by_id"]
            )
        },
        RegisteredFixture(id: "native.moderation.vote-integrity.penalties.revoke") {
            try assertFixtureCoversDTO($0, as: VoteIntegrityPenaltyEnvelope.self)
        }
    ]

private let voteIntegrityEnvelopeNullTargets: Set<String> = [
    "flag.agent_moderation_id",
    "flag.entity_relation_id",
    "flag.hostname_id",
    "flag.rss_feed_item_id",
    "flag.topic_id"
]

private let reportIntegrityEnvelopeNullTargets: Set<String> = [
    "flag.hostname_id",
    "flag.reported_user_id",
    "flag.rss_feed_item_id"
]
