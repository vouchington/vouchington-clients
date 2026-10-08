import VouchaAPI
import VouchaModels

let entityProvenanceFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "entity-provenance.communities") {
        try assertFixtureCoversDTO($0, as: CommunitiesSearchResponse.self)
    },
    RegisteredFixture(id: "entity-provenance.topics") {
        try assertFixtureCoversDTO($0, as: TopicSearchResponse.self)
    },
    RegisteredFixture(id: "entity-provenance.lists") {
        try assertFixtureCoversDTO($0, as: ListsSearchResponse.self)
    },
    RegisteredFixture(id: "entity-provenance.rss-feeds") {
        try assertFixtureCoversDTO($0, as: RssFeedSourceListResponse.self)
    }
]

let entityProvenanceFixtureEndpoints: [String: Endpoint] = [
    "entity-provenance.communities": .communities(query: "test"),
    "entity-provenance.topics": .topics(query: "test"),
    "entity-provenance.lists": .lists(),
    "entity-provenance.rss-feeds": .allRssFeeds(limit: 25)
]
