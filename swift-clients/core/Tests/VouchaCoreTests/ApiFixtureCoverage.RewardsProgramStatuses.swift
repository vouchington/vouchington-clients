import VouchaModels

let rewardsProgramStatusFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.rewards-program-status-topics.search.default") {
        try assertFixtureCoversDTO($0, as: TopicSearchResponse.self)
    },
    RegisteredFixture(id: "native.rewards-statuses.empty") {
        try assertFixtureCoversDTO($0, as: RewardsProgramStatusPage.self)
    },
    RegisteredFixture(id: "native.rewards-statuses.page-1") {
        try assertFixtureCoversDTO($0, as: RewardsProgramStatusPage.self)
    },
    RegisteredFixture(id: "native.rewards-statuses.page-2") {
        try assertFixtureCoversDTO($0, as: RewardsProgramStatusPage.self)
    },
    RegisteredFixture(id: "native.rewards-statuses.create.default") {
        try assertFixtureCoversDTO($0, as: RewardsProgramStatusResponse.self)
    },
    RegisteredFixture(id: "native.rewards-statuses.update.full") {
        try assertFixtureCoversDTO($0, as: RewardsProgramStatusResponse.self)
    },
    RegisteredFixture(id: "native.rewards-statuses.update.clear-dates") {
        try assertFixtureCoversDTO($0, as: RewardsProgramStatusResponse.self)
    }
]
