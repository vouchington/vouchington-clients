import Foundation
import VouchaAPI
import VouchaCore
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
}
