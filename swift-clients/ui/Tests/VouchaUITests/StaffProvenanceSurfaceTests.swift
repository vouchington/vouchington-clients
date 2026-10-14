import ViewInspector
@testable import VouchaFeatures
import XCTest

@MainActor
final class StaffProvenanceSurfaceTests: NativeRouteSurfaceViewModelTestCase {
    func testPostBrowseStoryBrowseAndFeedRowsRetainStaffChannel() async throws {
        let fixture = ApiFixtureLoader.data("native.users.profile.posts.staff-provenance")
        let cases: [(NativeRouteDestinationIdentifier, String)] = [
            (.postsBrowse, "/api/v1/posts"),
            (.storiesBrowse, "/api/v1/posts"),
            (.feedPosts, "/api/v1/feeds/posts/any")
        ]
        for (destination, path) in cases {
            CannedFeedURLProtocol.reset()
            CannedFeedURLProtocol.handlers[path] = (fixture, 200)
            let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: destination), client: makeClient())
            let page = try await viewModel.loadPostPage(client: makeClient(), after: nil)

            XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, path)
            let row = try XCTUnwrap(page.rows.first { $0.id == "profile-mcp-1" }).row
            XCTAssertEqual(row.staffProvenance?.createdVia, "mcp")
            XCTAssertThrowsError(try NativeSurfaceRow(row: row).inspect().find(text: "Channel: mcp"))
            XCTAssertNoThrow(try NativeSurfaceRow(row: row, isStaffViewer: true).inspect().find(text: "Channel: mcp"))
        }
    }

    func testProfileStaffPostFixtureShowsRawChannelOnlyToStaff() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (
            ApiFixtureLoader.data("native.users.profile.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/posts"] = (
            ApiFixtureLoader.data("native.users.profile.posts.staff-provenance"), 200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice/posts"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry, client: makeClient(), routeMatch: route.match
        )
        await viewModel.load()
        let member = NativeUserProfileSurface(
            entry: route.entry, viewModel: viewModel, isSignedIn: true,
            isStaffViewer: false, turnstileSiteKey: nil, showSignIn: {}, onNavigate: { _ in }
        )
        let staff = NativeUserProfileSurface(
            entry: route.entry, viewModel: viewModel, isSignedIn: true,
            isStaffViewer: true, turnstileSiteKey: nil, showSignIn: {}, onNavigate: { _ in }
        )

        XCTAssertThrowsError(try member.inspect().find(text: "Channel: mcp"))
        XCTAssertNoThrow(try staff.inspect().find(text: "Channel: mcp"))
        XCTAssertThrowsError(try staff.inspect().find(text: "Client: Fixture Agent (voucha_fixture_agent)"))
    }
}
