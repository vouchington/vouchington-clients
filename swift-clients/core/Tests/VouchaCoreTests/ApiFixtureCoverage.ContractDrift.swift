import VouchaAPI
import VouchaModels

let contractDriftFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.moderation.report-integrity.flag") {
        try assertFixtureCoversDTO($0, as: ReportIntegrityFlagEnvelope.self)
    },
    RegisteredFixture(id: "native.moderation.report-integrity.penalties.review-due") {
        try assertFixtureCoversDTO($0, as: ReportIntegrityPenaltiesResponse.self)
    },
    RegisteredFixture(id: "native.moderation.report-integrity.penalties.review") {
        try assertFixtureCoversDTO($0, as: ReportIntegrityPenaltyEnvelope.self)
    },
    RegisteredFixture(id: "native.moderation.vote-integrity.penalties.review-due") {
        try assertFixtureCoversDTO($0, as: VoteIntegrityPenaltiesResponse.self)
    },
    RegisteredFixture(id: "native.moderation.vote-integrity.penalties.review") {
        try assertFixtureCoversDTO($0, as: VoteIntegrityPenaltyEnvelope.self)
    },
    RegisteredFixture(id: "native.moderation.report-integrity.penalties.review-due.page-1") {
        try assertFixtureCoversDTO($0, as: ReportIntegrityPenaltiesResponse.self)
    },
    RegisteredFixture(id: "native.moderation.report-integrity.penalties.review-due.page-2") {
        try assertFixtureCoversDTO($0, as: ReportIntegrityPenaltiesResponse.self)
    },
    RegisteredFixture(id: "native.moderation.vote-integrity.penalties.review-due.page-1") {
        try assertFixtureCoversDTO($0, as: VoteIntegrityPenaltiesResponse.self)
    },
    RegisteredFixture(id: "native.moderation.vote-integrity.penalties.review-due.page-2") {
        try assertFixtureCoversDTO($0, as: VoteIntegrityPenaltiesResponse.self)
    },
    RegisteredFixture(id: "native.copyright.repeat-infringer-accounts") {
        try assertFixtureCoversDTO($0, as: CopyrightRepeatInfringerAccountsResponse.self)
    },
    RegisteredFixture(id: "web.copyright.staff-queue.default") {
        try assertFixtureCoversDTO($0, as: CopyrightStaffQueueResponse.self)
    },
    RegisteredFixture(id: "web.copyright.email-notice-association-search") {
        try assertFixtureCoversDTO($0, as: CopyrightAssociationTargetsResponse.self)
    },
    RegisteredFixture(id: "web.admin.psql.jobs.classifier-range") {
        try assertFixtureCoversDTO($0, as: EngineeringSuccessResponse.self)
    }
]
