import VouchaModels

let nativeMembershipStoreFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.memberships.me.default") {
        try assertFixtureCoversDTO($0, as: DecodedJSONValue.self)
    },
    RegisteredFixture(id: "native.memberships.me.lifecycle.default") {
        try assertFixtureCoversDTO($0, as: DecodedJSONValue.self)
    },
    RegisteredFixture(id: "native.memberships.purchase-intent.apple.default") {
        try assertFixtureCoversDTO($0, as: DecodedJSONValue.self)
    },
    RegisteredFixture(id: "native.memberships.purchase-intent.conflict.default") {
        try assertFixtureCoversDTO($0, as: DecodedJSONValue.self)
    },
    RegisteredFixture(id: "native.memberships.purchase-intent.google.default") {
        try assertFixtureCoversDTO($0, as: DecodedJSONValue.self)
    },
    RegisteredFixture(id: "native.memberships.purchase-intent.microsoft.default") {
        try assertFixtureCoversDTO($0, as: DecodedJSONValue.self)
    },
    RegisteredFixture(id: "native.memberships.purchase-intent.stripe.default") {
        try assertFixtureCoversDTO($0, as: DecodedJSONValue.self)
    },
    RegisteredFixture(id: "native.memberships.verification.pending.default") {
        try assertFixtureCoversDTO($0, as: DecodedJSONValue.self)
    },
    RegisteredFixture(id: "native.memberships.verification-status.conflict.default") {
        try assertFixtureCoversDTO($0, as: DecodedJSONValue.self)
    },
    RegisteredFixture(id: "native.memberships.verification-status.pending.default") {
        try assertFixtureCoversDTO($0, as: DecodedJSONValue.self)
    },
    RegisteredFixture(id: "native.memberships.verification-status.rejected.default") {
        try assertFixtureCoversDTO($0, as: DecodedJSONValue.self)
    },
    RegisteredFixture(id: "native.memberships.verification-status.verified.default") {
        try assertFixtureCoversDTO($0, as: DecodedJSONValue.self)
    }
]
