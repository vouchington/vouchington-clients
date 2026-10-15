import Foundation
import ViewInspector
import VouchaAPI
import VouchaCore
import VouchaDesignSystem
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeUserProfileTopicHostTests: NativeRouteSurfaceViewModelTestCase {
    func testProfileTopicLogoUsesActiveClientsImageHost() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (
            ApiFixtureLoader.data("native.users.profile.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/topics/following"] = (
            ApiFixtureLoader.data("native.users.profile.topics-following.first-page"), 200
        )
        let config = try AppConfig(
            baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
            imageBaseURL: XCTUnwrap(URL(string: "https://images.self-hosted.test"))
        )
        let client = APIClient(
            config: config,
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice/topics/following"))
        let viewModel = NativeRouteSurfaceViewModel(
            entry: route.entry, client: client, routeMatch: route.match
        )
        await viewModel.load()
        let surface = NativeUserProfileSurface(
            entry: route.entry,
            viewModel: viewModel,
            isSignedIn: true,
            turnstileSiteKey: nil,
            showSignIn: {},
            onNavigate: { _ in }
        )

        let row = try surface.inspect().find(viewWithAccessibilityIdentifier: "public-profile-topic-row")
        let image = try row.find(ViewType.View<AsyncImageView>.self)
        XCTAssertEqual(
            try image.actualView().resolvedURLString,
            "https://images.self-hosted.test/images/placements/00000000-0000-7000-8000-000000000306/3/00000000-0000-7000-8000-000000000205?w=80"
        )
        XCTAssertTrue(try image.accessibilityHidden())
        XCTAssertNoThrow(try row.find(text: "Test Topic"))
    }
}
