import VouchaModels

let nativeCommentAncestorCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.comments.ancestors.bounded.shallow") {
        try assertFixtureCoversDTO($0, as: DecodedJSONValue.self)
    },
    RegisteredFixture(id: "native.comments.ancestors.bounded.deep-initial") {
        try assertFixtureCoversDTO($0, as: DecodedJSONValue.self)
    },
    RegisteredFixture(id: "native.comments.ancestors.bounded.deep-continuation") {
        try assertFixtureCoversDTO($0, as: DecodedJSONValue.self)
    }
]
