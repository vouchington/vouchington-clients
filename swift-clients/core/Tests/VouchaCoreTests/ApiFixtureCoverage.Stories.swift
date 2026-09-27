import VouchaModels

let storyFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.stories.get.default") {
        try assertFixtureCoversDTO($0, as: StoryPageResponse.self)
    },
    RegisteredFixture(id: "native.stories.get.after") {
        try assertFixtureCoversDTO($0, as: StoryPageResponse.self)
    }
]
