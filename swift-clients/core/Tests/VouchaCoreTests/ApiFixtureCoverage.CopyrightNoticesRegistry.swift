import VouchaModels

let copyrightNoticesFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "web.copyright.notices.default") {
        try assertFixtureCoversDTO($0, as: CopyrightNoticesResponse.self)
    },
    RegisteredFixture(id: "web.copyright.notices.null-claimant") {
        try assertFixtureCoversDTO($0, as: CopyrightNoticesResponse.self)
    },
    RegisteredFixture(id: "web.copyright.notice.detail.populated") {
        try assertFixtureCoversDTO($0, as: CopyrightNoticeResponse.self)
    },
    RegisteredFixture(id: "web.copyright.notice.detail.null-claimant") {
        try assertFixtureCoversDTO($0, as: CopyrightNoticeResponse.self)
    },
    RegisteredFixture(id: "web.copyright.notice.participant.populated") {
        try assertFixtureCoversDTO($0, as: CopyrightNoticeResponse.self)
    },
    RegisteredFixture(id: "web.copyright.notice.participant.null-claimant") {
        try assertFixtureCoversDTO($0, as: CopyrightNoticeResponse.self)
    },
    RegisteredFixture(id: "web.copyright.eu.participant.no-action-complaint") {
        try assertFixtureCoversDTO($0, as: CopyrightNoticeResponse.self)
    },
    RegisteredFixture(id: "web.copyright.eu.dispute-settlements.participant") {
        try assertFixtureCoversDTO($0, as: CopyrightEuDisputeSettlementsResponse.self)
    }
]
