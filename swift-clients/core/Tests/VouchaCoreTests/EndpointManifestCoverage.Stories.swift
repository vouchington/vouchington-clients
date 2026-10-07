import VouchaAPI

extension EndpointManifestCoverage {
    static let storyEndpoints: [ManifestRegisteredEndpoint] = [
        ManifestRegisteredEndpoint(id: "native.stories.get.default") { StoryFixture.firstEndpoint },
        ManifestRegisteredEndpoint(id: "native.stories.get.after") { StoryFixture.nextEndpoint }
    ]
}

extension ApiFixtureEndpointCoverageTests {
    static let storyFixtureEndpoints: [String: Endpoint] = [
        "native.stories.get.default": StoryFixture.firstEndpoint,
        "native.stories.get.after": StoryFixture.nextEndpoint
    ]
}
