import VouchaModels

let nativeMembershipStoreFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.memberships.me.default") {
        try assertFixtureCoversDTO($0, as: DecodedJSONValue.self)
    }
]
