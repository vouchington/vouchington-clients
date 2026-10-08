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
        entityProvenanceFixtureCoverage +
        webCommunityApiFixtureCoverage +
        householdFixtureCoverage +
        paymentCardFixtureCoverage +
        pointValuationFixtureCoverage +
        spendingCategoryFixtureCoverage +
        rewardsProgramStatusFixtureCoverage +
        currencyFixtureCoverage +
        credentialFixtureCoverage +
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
        storyFixtureCoverage +
        nativeOAuthAndFriendRecommendationFixtureCoverage
        + copyrightMediaFixtureCoverage
        + chatFixtureCoverage
}

let chatFixtureCoverage: [RegisteredFixture] = [
    "native.chat.completed", "native.chat.duplicate", "native.chat.retry"
].map { id in
    RegisteredFixture(id: id) { try assertFixtureCoversDTO($0, as: ClientGeneratedChatResponse.self) }
} + [
    "native.chat.page-1", "native.chat.page-2", "native.chat.incomplete"
].map { id in
    RegisteredFixture(id: id) { try assertFixtureCoversDTO($0, as: ChatMessagesResponse.self) }
} + [
    RegisteredFixture(id: "native.chat.conversations") {
        try assertFixtureCoversDTO($0, as: ChatConversationListResponse.self)
    },
    RegisteredFixture(id: "native.chat.unauthorized") {
        try assertFixtureCoversDTO($0, as: ChatErrorResponse.self)
    },
    RegisteredFixture(id: "native.chat.forbidden") {
        try assertFixtureCoversDTO($0, as: ChatErrorResponse.self)
    },
    RegisteredFixture(id: "native.chat.identity-conflict") {
        try assertFixtureCoversDTO($0, as: ChatErrorResponse.self)
    },
    RegisteredFixture(id: "native.moderation.appeals.detail.default") {
        try assertFixtureCoversDTO($0, as: ModerationAppealEnvelope.self)
    },
    RegisteredFixture(id: "native.my.api-keys.rotate") {
        try assertFixtureCoversDTO($0, as: ApiKeyCreationResponse.self)
    },
    RegisteredFixture(id: "web.communities.automod-settings.update.default") {
        try assertFixtureCoversDTO($0, as: CommunityResponse.self)
    }
]
