import VouchaModels

let moderationRunFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.moderation.report-judgement-run.succeeded") {
        try assertFixtureCoversDTO($0, as: ModerationReportJudgementRunResponse.self)
    },
    RegisteredFixture(id: "native.moderation.report-judgement-run.failed") {
        try assertFixtureCoversDTO($0, as: ModerationReportJudgementRunResponse.self)
    },
    RegisteredFixture(id: "native.moderation.report-judgement-run.expired") {
        try assertFixtureCoversDTO($0, as: ModerationReportJudgementRunResponse.self)
    },
    RegisteredFixture(id: "native.moderation.report-detail.pending") {
        try assertFixtureCoversDTO(
            $0,
            as: ModerationReportDetailResponse.self,
            ignoring: ["report.note", "report.resolved_by_id", "report.reviewed_at"]
        )
    },
    RegisteredFixture(id: "native.moderation.report-detail.saved") {
        try assertFixtureCoversDTO(
            $0,
            as: ModerationReportDetailResponse.self,
            ignoring: ["report.note", "report.resolved_by_id", "report.reviewed_at"]
        )
    }
]
