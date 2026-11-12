import Foundation
import ViewInspector
import VouchaAPI
import VouchaCore
import VouchaDesignSystem
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class FriendsAvatarPlacementTests: XCTestCase {
    func testAvatarURLRequiresReturnedPlacementDescriptor() throws {
        let config = try AppConfig(
            baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
            imageBaseURL: XCTUnwrap(URL(string: "https://images.voucha.ai")),
            turnstileSiteKey: "test-site-key"
        )
        let client = APIClient(
            config: config,
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        let viewModel = FriendsListViewModel(client: client, userId: "u", config: config)
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        let withPlacement = try decoder.decode(
            PublicUser.self,
            from: Data(
                #"{"id":"u1","username":"alice","roles":[],"profile_image_id":"img-abc","profile_image_placement":{"image_id":"img-abc","placement_id":"placement-abc","placement_revision":2}}"#
                    .utf8
            )
        )
        let withoutPlacement = try decoder.decode(
            PublicUser.self,
            from: Data(#"{"id":"u2","username":"bob","roles":[],"profile_image_id":"legacy-id"}"#.utf8)
        )

        XCTAssertEqual(
            viewModel.avatarURL(for: withPlacement),
            "https://images.voucha.ai/images/placements/placement-abc/2/img-abc?w=96"
        )
        XCTAssertNil(viewModel.avatarURL(for: withoutPlacement))
    }

    func testLoadedFriendRowsRenderPlacementUsingConfiguredHost() async throws {
        for host in ["https://images.voucha.ai", "https://replacement.images.test"] {
            let view = try await loadedFriendsView(imageHost: host)
            let row = try view.inspect().find(UserRow.self).actualView()
            let avatar = try row.inspect().find(Avatar.self).actualView()
            XCTAssertEqual(
                avatar.imageURL,
                "\(host)/images/placements/00000000-0000-7000-8000-000000000301/2/00000000-0000-7000-8000-000000000201?w=96"
            )
            XCTAssertNoThrow(try view.inspect().find(text: "Friendly User"))
        }
    }

    func testMissingAndWithheldFriendPlacementsRenderNamedInitialsWithoutLegacyRequests() async throws {
        for retainsLegacyId in [false, true] {
            let view = try await loadedFriendsView(imageHost: "https://replacement.images.test") { user in
                user["profile_image_placement"] = NSNull()
                if !retainsLegacyId { user["profile_image_id"] = NSNull() }
            }
            let row = try view.inspect().find(UserRow.self).actualView()
            let avatar = try row.inspect().find(Avatar.self).actualView()
            XCTAssertNil(avatar.imageURL)
            XCTAssertEqual(avatar.username, "Friendly User")
            XCTAssertTrue(try avatar.inspect().findAll(ViewType.AsyncImage.self).isEmpty)
            XCTAssertNoThrow(try avatar.inspect().find(text: "FU"))
            XCTAssertNoThrow(try view.inspect().find(text: "Friendly User"))
        }
    }

    private func loadedFriendsView(
        imageHost: String,
        mutate: (inout [String: Any]) -> Void = { _ in }
    ) async throws -> FriendsListView {
        var fixture = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("swift.users.following.default")
        ) as? [String: Any])
        var users = try XCTUnwrap(fixture["results"] as? [[String: Any]])
        mutate(&users[0])
        fixture["results"] = users
        CannedFeedURLProtocol.handlers = try [
            "/api/v1/users/owner/users/following": (JSONSerialization.data(withJSONObject: fixture), 200)
        ]
        defer { CannedFeedURLProtocol.handlers = [:] }
        let config = try AppConfig(
            baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
            imageBaseURL: XCTUnwrap(URL(string: imageHost)),
            turnstileSiteKey: "test-site-key"
        )
        let client = APIClient(
            config: config,
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        let model = FriendsListViewModel(client: client, userId: "owner", config: config)
        await model.load()
        XCTAssertEqual(model.items.count, 1)
        return FriendsListView(viewModel: model)
    }

}
