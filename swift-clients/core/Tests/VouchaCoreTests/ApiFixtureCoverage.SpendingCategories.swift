import VouchaModels

let spendingCategoryFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.spending-category-topics.search.default") {
        try assertFixtureCoversDTO($0, as: TopicSearchResponse.self, ignoring: ["results.__entity_type"])
    },
    RegisteredFixture(id: "native.spending-categories.empty") {
        try assertFixtureCoversDTO($0, as: SpendingCategoryPage.self)
    },
    RegisteredFixture(id: "native.spending-categories.page-1") {
        try assertFixtureCoversDTO($0, as: SpendingCategoryPage.self)
    },
    RegisteredFixture(id: "native.spending-categories.owned-household") {
        try assertFixtureCoversDTO($0, as: SpendingCategoryPage.self)
    },
    RegisteredFixture(id: "native.spending-categories.page-2") {
        try assertFixtureCoversDTO($0, as: SpendingCategoryPage.self)
    },
    RegisteredFixture(id: "native.spending-categories.create.default") {
        try assertFixtureCoversDTO($0, as: SpendingCategoryResponse.self)
    },
    RegisteredFixture(id: "native.spending-categories.update.clear-note") {
        try assertFixtureCoversDTO($0, as: SpendingCategoryResponse.self)
    }
]
