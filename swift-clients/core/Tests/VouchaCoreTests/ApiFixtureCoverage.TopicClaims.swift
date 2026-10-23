import VouchaModels

let topicClaimFixtureCoverage: [RegisteredFixture] = [
    "native.my-topic-claims.page",
    "native.topic-claims.page"
].map { id in
    RegisteredFixture(id: id) {
        try assertFixtureCoversDTO($0, as: Page<TopicClaim>.self)
    }
}
