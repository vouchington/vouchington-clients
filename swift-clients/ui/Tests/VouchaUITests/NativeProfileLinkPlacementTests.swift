import ViewInspector
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class NativeProfileLinkPlacementTests: NativeRouteSurfaceViewModelTestCase {
    func testCanonicalProfileLinkPlacementRendersWithoutLegacyImageURL() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (
            ApiFixtureLoader.data("native.users.profile.default"), 200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice"))
        let model = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )
        await model.load()
        let placement = try XCTUnwrap(model.userProfile.header?.profileLinks.first?.imagePlacement)
        XCTAssertEqual(placement.placementId, "00000000-0000-7000-8000-000000000302")
        let surface = NativeUserProfileSurface(
            entry: route.entry,
            viewModel: model,
            isSignedIn: false,
            turnstileSiteKey: nil,
            showSignIn: {},
            onNavigate: { _ in }
        )

        let image = try surface.inspect().find(ProfileLinkPlacementImage.self).actualView()
        XCTAssertEqual(
            image.url.absoluteString,
            "https://images.voucha.ai/images/placements/00000000-0000-7000-8000-000000000302/1/00000000-0000-7000-8000-000000000202?w=48"
        )
        XCTAssertNoThrow(try surface.inspect().find(viewWithAccessibilityIdentifier: "public-profile-link-image"))
    }

    func testMissingLinkLabelUsesDestinationHostRatherThanPlacementHost() async throws {
        var root = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("native.users.profile.default")
        ) as? [String: Any])
        var links = try XCTUnwrap(root["profile_links"] as? [[String: Any]])
        links[0]["name"] = NSNull()
        links[0]["handle"] = NSNull()
        root["profile_links"] = links
        let fixture = try JSONSerialization.data(withJSONObject: root)
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (fixture, 200)
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice"))
        let model = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )
        await model.load()
        let surface = NativeUserProfileSurface(
            entry: route.entry, viewModel: model, isSignedIn: false,
            turnstileSiteKey: nil, showSignIn: {}, onNavigate: { _ in }
        )

        XCTAssertNoThrow(try surface.inspect().find(text: "example.test"))
        XCTAssertThrowsError(try surface.inspect().find(text: "images.voucha.ai"))
    }
}
