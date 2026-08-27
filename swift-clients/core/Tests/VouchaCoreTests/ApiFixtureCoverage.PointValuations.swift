import VouchaModels

let pointValuationFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.rewards-program-topics.search.default") {
        try assertFixtureCoversDTO($0, as: TopicSearchResponse.self)
    },
    RegisteredFixture(id: "native.point-valuations.empty") {
        try assertFixtureCoversDTO($0, as: PointValuationPage.self)
    },
    RegisteredFixture(id: "native.point-valuations.page-1") {
        try assertFixtureCoversDTO($0, as: PointValuationPage.self)
    },
    RegisteredFixture(id: "native.point-valuations.page-2") {
        try assertFixtureCoversDTO($0, as: PointValuationPage.self)
    },
    RegisteredFixture(id: "native.point-valuations.create.default") {
        try assertFixtureCoversDTO($0, as: PointValuationResponse.self)
    },
    RegisteredFixture(id: "native.point-valuations.update.full") {
        try assertFixtureCoversDTO($0, as: PointValuationResponse.self)
    },
    RegisteredFixture(id: "native.point-valuations.update.clear-note") {
        try assertFixtureCoversDTO($0, as: PointValuationResponse.self)
    }
]
