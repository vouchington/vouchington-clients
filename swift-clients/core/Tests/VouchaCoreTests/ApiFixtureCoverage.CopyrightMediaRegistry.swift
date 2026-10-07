import VouchaModels

let copyrightMediaFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.posts.images.placement.default") {
        try assertFixtureCoversDTO($0, as: PostImagePlacementResponse.self)
    },
    RegisteredFixture(id: "native.moderation.copyright.image-similarity-candidates.default") {
        try assertFixtureCoversDTO($0, as: CopyrightSimilarityCandidatesResponse.self)
    }
]
