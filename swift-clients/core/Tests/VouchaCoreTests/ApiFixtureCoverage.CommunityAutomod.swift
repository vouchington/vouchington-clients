import VouchaAPI
import VouchaModels

let communityAutomodFixtureCoverage: [RegisteredFixture] = ["page-1", "page-2", "member"].map { page in
    RegisteredFixture(id: "native.communities.moderation-queue.automod-flag.\(page)") {
        try assertFixtureCoversDTO($0, as: CommunityModerationQueueResponse.self)
    }
}

let communityAutomodFixtureEndpoints: [String: Endpoint] = [
    "native.communities.moderation-queue.automod-flag.page-1": .communityModerationQueue(
        idOrSlug: "test-community", limit: 1, source: .automodFlag
    ),
    "native.communities.moderation-queue.automod-flag.page-2": .communityModerationQueue(
        idOrSlug: "test-community",
        after: "eyJjcmVhdGVkX2F0IjoiMjAyNi0wMS0wMVQwMDowMDowMC4wMDAwMDBaIiwiaWQiOiIwMDAwMDAwMC0wMDAwLTcwMDAtODAwMC0wMDAwMDAwMDA5MDIifQ",
        limit: 1, source: .automodFlag
    ),
    "native.communities.moderation-queue.automod-flag.member": .communityModerationQueue(
        idOrSlug: "test-community", limit: 1, source: .automodFlag
    ),
    "native.communities.automod-flag.dismissal.default": .dismissCommunityAutomodFlag(
        idOrSlug: "test-community", postId: "00000000-0000-7000-8000-000000000902"
    )
]
