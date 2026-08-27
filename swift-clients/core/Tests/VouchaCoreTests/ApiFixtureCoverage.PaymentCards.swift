import VouchaModels

let paymentCardFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.card-topics.search.default") {
        try assertFixtureCoversDTO($0, as: TopicSearchResponse.self)
    },
    RegisteredFixture(id: "native.cards.empty") {
        try assertFixtureCoversDTO($0, as: PaymentCardPage.self)
    },
    RegisteredFixture(id: "native.cards.page-1") {
        try assertFixtureCoversDTO($0, as: PaymentCardPage.self)
    },
    RegisteredFixture(id: "native.cards.page-2") {
        try assertFixtureCoversDTO($0, as: PaymentCardPage.self)
    },
    RegisteredFixture(id: "native.cards.create.default") {
        try assertFixtureCoversDTO($0, as: PaymentCardResponse.self)
    },
    RegisteredFixture(id: "native.cards.update.full") {
        try assertFixtureCoversDTO($0, as: PaymentCardResponse.self)
    },
    RegisteredFixture(id: "native.cards.update.clear") {
        try assertFixtureCoversDTO($0, as: PaymentCardResponse.self)
    }
]
