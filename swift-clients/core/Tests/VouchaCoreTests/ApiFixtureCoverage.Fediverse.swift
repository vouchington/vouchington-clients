import VouchaModels

private let directoryIgnoredFields: Set<String> = [
    "bookmarks", "election_votes", "markdown_to_html", "results.__entity_type", "topics_metrics",
    "markdown_to_html.00000000-0000-7000-8000-00000000f001",
    "topic_elections.00000000-0000-7000-8000-00000000f001.__entity_type",
    "topic_elections.00000000-0000-7000-8000-00000000f001.id",
    "hostname_elections.00000000-0000-7000-8000-00000000f002.__entity_type",
    "topics.00000000-0000-7000-8000-00000000f001.hostname.__entity_type",
    "topics.00000000-0000-7000-8000-00000000f003.hostname.__entity_type",
    "topics.00000000-0000-7000-8000-00000000f005.hostname.__entity_type"
]

let fediverseFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.fediverse.instances.default") {
        try assertFixtureCoversDTO($0, as: FediverseInstancesResponse.self, ignoring: directoryIgnoredFields)
    },
    RegisteredFixture(id: "native.fediverse.instances.page-2") {
        try assertFixtureCoversDTO($0, as: FediverseInstancesResponse.self, ignoring: directoryIgnoredFields)
    },
    RegisteredFixture(id: "native.fediverse.instances.empty") {
        try assertFixtureCoversDTO($0, as: FediverseInstancesResponse.self, ignoring: directoryIgnoredFields)
    },
    RegisteredFixture(id: "native.fediverse.instance.slug") {
        try assertFixtureCoversDTO($0, as: FediverseInstanceDetailResponse.self, ignoring: [
            "topic.hostname.__entity_type", "topic_election.__entity_type", "topic_election.id",
            "hostname_election.__entity_type"
        ])
    },
    RegisteredFixture(id: "native.fediverse.instance.uuid") {
        try assertFixtureCoversDTO($0, as: FediverseInstanceDetailResponse.self, ignoring: [
            "topic.hostname.__entity_type", "topic_election.__entity_type", "topic_election.id",
            "hostname_election.__entity_type"
        ])
    },
    RegisteredFixture(id: "native.auth.bluesky.link.native") {
        try assertFixtureCoversDTO($0, as: BeginBlueskyAccountLinkResponse.self)
    }
]
