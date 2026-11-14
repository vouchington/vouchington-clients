import Foundation
import ViewInspector
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class NativeUserProfileReferralLinksTests: NativeRouteSurfaceViewModelTestCase {
    func testOtherUserProfileLoadsPublicRowsAndOpensReferralProgram() async throws {
        let loaded = try await load(fixture: "native.referral-links.user.public")

        let request = try XCTUnwrap(
            CannedFeedURLProtocol.capturedURLs.first { $0.path == "/api/v1/referral-links" }
        )
        let items = URLComponents(url: request, resolvingAgainstBaseURL: false)?.queryItems ?? []
        XCTAssertEqual(items.first { $0.name == "user_id" }?.value, "user-abc")
        XCTAssertEqual(items.first { $0.name == "limit" }?.value, "25")
        guard case let .referralLinks(links) = loaded.viewModel.userProfile.collection else {
            return XCTFail("expected referral link rows")
        }
        XCTAssertEqual(links.map(\.id), ["referral-link-2"])
        XCTAssertNil(links.first?.label)
        XCTAssertEqual(
            links.first.flatMap(NativeUserProfileNavigationTarget.referralLink),
            "/referral-program/test-card"
        )
    }

    func testOwnerShapeRowsRenderLabelAndNavigate() async throws {
        let loaded = try await load(fixture: "native.referral-links.mine.default")
        var navigated: [String] = []
        let surface = loaded.surface { navigated.append($0) }

        let row = try surface.inspect().find(viewWithAccessibilityIdentifier: "public-profile-referral-link-row")
        XCTAssertNoThrow(try row.find(text: "My test card link"))
        try row.find(ViewType.Button.self).tap()
        XCTAssertEqual(navigated, ["/referral-program/test-card"])
    }

    func testReferralLinksTabIsListedAndSelectedOnItsRoute() async throws {
        let loaded = try await load(fixture: "native.referral-links.user.public")
        let header = try XCTUnwrap(loaded.viewModel.userProfile.header)

        let tab = try XCTUnwrap(
            loaded.surface().visiblePrimaryTabs(header).first { $0.title == .nativeSwiftLandingPagesReferralLinks }
        )
        XCTAssertEqual(tab.path, "/user/alice/referral-links")
        XCTAssertTrue(tab.selected)
    }

    func testRowWithoutProgramIdHasNoNavigationTarget() throws {
        let link = try JSONDecoder().decode(NativeReferralLink.self, from: Data(#"{"id":"link-1"}"#.utf8))
        XCTAssertNil(NativeUserProfileNavigationTarget.referralLink(link))
        XCTAssertEqual(NativeUserProfileReferralLinkRow(link: link, onOpen: {}).title, .verbatim("link-1"))
    }

    func testLoadMoreKeepsSingleRowWhenNoNextPage() async throws {
        let loaded = try await load(fixture: "native.referral-links.user.public")
        await loaded.viewModel.loadMoreUserProfileRows()
        XCTAssertEqual(loaded.viewModel.userProfile.collection.count, 1)
    }

    private struct Loaded {
        let entry: NativeRouteCatalogEntry
        let viewModel: NativeRouteSurfaceViewModel

        func surface(onNavigate: @escaping (String) -> Void = { _ in }) -> NativeUserProfileSurface {
            NativeUserProfileSurface(
                entry: entry,
                viewModel: viewModel,
                isSignedIn: true,
                turnstileSiteKey: nil,
                showSignIn: {},
                onNavigate: onNavigate
            )
        }
    }

    private func load(fixture: String) async throws -> Loaded {
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (
            ApiFixtureLoader.data("native.users.profile.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/referral-links"] = (ApiFixtureLoader.data(fixture), 200)
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice/referral-links"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry, client: makeClient(), routeMatch: route.match
        )
        await viewModel.load()
        return Loaded(entry: route.entry, viewModel: viewModel)
    }
}
