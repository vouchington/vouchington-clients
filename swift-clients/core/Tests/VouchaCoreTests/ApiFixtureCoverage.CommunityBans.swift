@testable import VouchaAPI
import VouchaModels

let communityBanFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.communities.bans.active.default") {
        try assertFixtureCoversDTO($0, as: CommunityBansResponse.self, ignoring: [
            "results.__entity_type", "page_info.end_cursor",
            "users.00000000-0000-7000-8000-000000000904.profile_image_id",
            "community_bans.00000000-0000-7000-8000-000000000901.expires_at",
            "community_bans.00000000-0000-7000-8000-000000000901.lifted_at",
            "community_bans.00000000-0000-7000-8000-000000000901.lifted_by_id"
        ])
    },
    RegisteredFixture(id: "native.communities.bans.detail.default") {
        try assertFixtureCoversDTO($0, as: CommunityBanResponse.self, ignoring: [
            "community_ban.expires_at", "community_ban.lifted_at", "community_ban.lifted_by_id"
        ])
    },
    RegisteredFixture(id: "native.communities.bans.lift-one.default") {
        try assertFixtureCoversDTO($0, as: CommunityBanLiftResponse.self)
    },
    RegisteredFixture(id: "native.communities.bans.lift-all.default") {
        try assertFixtureCoversDTO($0, as: CommunityBanLiftResponse.self)
    },
    RegisteredFixture(id: "native.communities.bans.lift-all.forbidden") {
        try assertFixtureCoversDTO($0, as: BanErrorEnvelope.self)
    },
    RegisteredFixture(id: "native.communities.bans.lift-all.overflow") {
        try assertFixtureCoversDTO($0, as: BanErrorEnvelope.self)
    }
]

private struct BanErrorEnvelope: Codable {
    let code: String
    let message: String
    let cap: Int?
}

extension ApiFixtureEndpointCoverageTests {
    static var communityBanFixtureEndpoints: [String: Endpoint] {
        let userId = "00000000-0000-7000-8000-000000000904"
        let banId = "00000000-0000-7000-8000-000000000901"
        return [
            "native.communities.bans.active.default": .activeCommunityBans(idOrSlug: "test-community", userId: userId),
            "native.communities.bans.detail.default": .communityBan(idOrSlug: "test-community", banId: banId),
            "native.communities.bans.lift-one.default": .liftCommunityBan(idOrSlug: "test-community", banId: banId),
            "native.communities.bans.lift-all.default": .liftAllCommunityBans(
                idOrSlug: "test-community",
                userId: userId
            ),
            "native.communities.bans.lift-all.forbidden": .liftAllCommunityBans(
                idOrSlug: "test-community",
                userId: userId
            ),
            "native.communities.bans.lift-all.overflow": .liftAllCommunityBans(
                idOrSlug: "test-community",
                userId: userId
            )
        ]
    }
}
