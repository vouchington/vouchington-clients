import VouchaModels

/// Fixture-id -> round-trip check driving `ApiFixtureCoverageTests`'s field-completeness gate
/// (#6773). Every shared fixture this client consumes must appear here or fail the coverage-gate
/// test.
struct RegisteredFixture {
    let id: String
    let run: (String) throws -> Void
}

/// Thin test-only envelope over `Profile` — wraps the real model's fields, does not duplicate
/// them — matching the `GET /api/v1/my/profile` response shape.
struct ProfileCoverageEnvelope: Codable {
    let profile: Profile
}

/// Thin test-only envelope over `PrivateUser` — wraps the real model's fields, does not
/// duplicate them — matching the `GET /api/v1/my/identity` response shape.
struct IdentityCoverageEnvelope: Codable {
    let identity: PrivateUser
}

let emailAddressFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.my.email-addresses.empty") {
        try assertFixtureCoversDTO($0, as: EmailAddressListResponse.self)
    },
    RegisteredFixture(id: "native.my.email-addresses.request.default") {
        try assertFixtureCoversDTO($0, as: EmailAddressVerificationRequestResponse.self)
    },
    RegisteredFixture(id: "native.my.email-addresses.verify.default") {
        try assertFixtureCoversDTO($0, as: EmailAddressListResponse.self)
    }
]

let currencyFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "shared.currencies.list.default") {
        try assertFixtureCoversDTO($0, as: Page<Currency>.self)
    }
]

enum ApiFixtureCoverage {
    static let registry: [RegisteredFixture] =
        webCommunityApiFixtureCoverage +
        householdFixtureCoverage +
        paymentCardFixtureCoverage +
        pointValuationFixtureCoverage +
        spendingCategoryFixtureCoverage +
        rewardsProgramStatusFixtureCoverage +
        currencyFixtureCoverage +
        emailAddressFixtureCoverage +
        fediverseFixtureCoverage +
        firstPagePaginationFixtureCoverage +
        relationApiFixtureCoverage +
        bookmarkReferralSwiftCoverage +
        bookmarkPostPaginationFixtureCoverage +
        nativeImportExportCoverage +
        nativeModerationCoverage +
        moderationAppealLifecycleFixtureCoverage +
        moderationParityFixtureCoverage +
        nativeListMessageCoverage +
        nativeCommentAncestorCoverage +
        nativeUrlApiFixtureCoverage +
        userProfileApiFixtureCoverage +
        nativeMembershipFixtureCoverage +
        nativeMembershipStoreFixtureCoverage +
        engineeringOpsApiFixtureCoverage +
        dynamicConfigApiFixtureCoverage +
        accountFeedFixtureCoverage +
        nativeOAuthAndFriendRecommendationFixtureCoverage
}
