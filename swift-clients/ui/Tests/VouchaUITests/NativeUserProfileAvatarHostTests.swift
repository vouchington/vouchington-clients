import Foundation
import ViewInspector
import VouchaAPI
import VouchaCore
import VouchaDesignSystem
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeUserProfileAvatarHostTests: NativeRouteSurfaceViewModelTestCase {
    private let placementPath =
        "/images/placements/00000000-0000-7000-8000-000000000301/2/00000000-0000-7000-8000-000000000201"

    func testHeaderAvatarAndLinkImageFollowTheActiveClientImageHost() async throws {
        for host in ["https://images.self-hosted.test", "https://replacement-images.test"] {
            let surface = try await loadSurface(imageHost: host, mutate: { _ in })

            let avatar = try surface.inspect().find(Avatar.self).actualView()
            XCTAssertEqual(avatar.imageURL, "\(host)\(placementPath)?w=144")
            let link = try surface.inspect().find(ProfileLinkPlacementImage.self).actualView()
            XCTAssertEqual(
                link.url.absoluteString,
                "\(host)/images/placements/00000000-0000-7000-8000-000000000302/1/00000000-0000-7000-8000-000000000202?w=48"
            )
        }
    }

    func testMissingWithheldAndAnonymousAvatarsNeverFallBackToLegacyImageIds() async throws {
        let missing = try await loadSurface(imageHost: "https://images.test") { user in
            user["profile_image_id"] = NSNull()
            user["profile_image_placement"] = NSNull()
        }
        XCTAssertNil(try missing.inspect().find(Avatar.self).actualView().imageURL)

        let withheld = try await loadSurface(imageHost: "https://images.test") { user in
            user["profile_image_id"] = "00000000-0000-7000-8000-000000000201"
            user["profile_image_placement"] = NSNull()
        }
        let withheldAvatar = try withheld.inspect().find(Avatar.self).actualView()
        XCTAssertNil(withheldAvatar.imageURL, "Legacy image IDs must not be used as avatar URLs")
        XCTAssertEqual(withheldAvatar.username, "alice")

        let anonymous = try await loadSurface(imageHost: "https://images.test") { user in
            user["username"] = NSNull()
            user["profile_image_id"] = NSNull()
            user["profile_image_placement"] = NSNull()
        }
        let anonymousAvatar = try anonymous.inspect().find(Avatar.self).actualView()
        XCTAssertNil(anonymousAvatar.imageURL)
        XCTAssertEqual(anonymousAvatar.username, "")
    }

    private func loadSurface(
        imageHost: String,
        mutate: (inout [String: Any]) -> Void
    ) async throws -> NativeUserProfileSurface {
        var root = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("native.users.profile.default")
        ) as? [String: Any])
        var user = try XCTUnwrap(root["user"] as? [String: Any])
        mutate(&user)
        root["user"] = user
        let body = try JSONSerialization.data(withJSONObject: root)
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (body, 200)
        let config = try AppConfig(
            baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
            imageBaseURL: XCTUnwrap(URL(string: imageHost))
        )
        let client = APIClient(
            config: config,
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice"))
        let viewModel = NativeRouteSurfaceViewModel(entry: route.entry, client: client, routeMatch: route.match)
        await viewModel.load()
        return NativeUserProfileSurface(
            entry: route.entry,
            viewModel: viewModel,
            isSignedIn: false,
            turnstileSiteKey: nil,
            showSignIn: {},
            onNavigate: { _ in }
        )
    }
}
