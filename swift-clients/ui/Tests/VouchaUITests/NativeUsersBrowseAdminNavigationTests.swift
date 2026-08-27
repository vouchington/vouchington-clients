import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeUsersBrowseAdminNavigationTests: NativeRouteSurfaceViewModelTestCase {
    func testAdministratorRowCarriesUsernameTarget() async throws {
        try await loadBrowse(isAdministrator: true, users: #"{"id":"user-1","username":"alice"}"#)

        XCTAssertEqual(viewModelRow?.targetPath, "/user/alice/admin")
        XCTAssertEqual(viewModelRow?.icon, "person.badge.shield.checkmark")
        XCTAssertEqual(viewModelRow?.detail, "Active")
    }

    func testAdministratorActiveRowShowsEmailWhenPresent() async throws {
        try await loadBrowse(
            isAdministrator: true,
            users: #"{"id":"user-1","username":"alice","email_address":"alice@example.com"}"#
        )

        XCTAssertEqual(viewModelRow?.detail, "Active\nalice@example.com")
        XCTAssertEqual(viewModelRow?.targetPath, "/user/alice/admin")
    }

    func testAdministratorSuspendedRowShowsStatus() async throws {
        try await loadBrowse(
            isAdministrator: true,
            users: #"{"id":"user-1","username":"alice","suspended_at":"2026-01-02T03:04:05Z"}"#
        )

        XCTAssertEqual(viewModelRow?.detail, "Suspended")
    }

    func testMissingUsernameFallsBackToId() async throws {
        try await loadBrowse(isAdministrator: true, users: #"{"id":"user-1"}"#)

        XCTAssertEqual(viewModelRow?.targetPath, "/user/user-1/admin")
    }

    func testEmptyUsernameFallsBackToId() async throws {
        try await loadBrowse(isAdministrator: true, users: #"{"id":"user-1","username":""}"#)

        XCTAssertEqual(viewModelRow?.targetPath, "/user/user-1/admin")
    }

    func testNonAdministratorRowsStayInertAndHideAdminFields() async throws {
        try await loadBrowse(
            isAdministrator: false,
            users: #"{"id":"user-1","username":"alice","email_address":"alice@example.com","suspended_at":"2026-01-02T03:04:05Z"}"#
        )

        XCTAssertNil(viewModelRow?.targetPath)
        XCTAssertEqual(viewModelRow?.icon, "person")
        XCTAssertEqual(viewModelRow?.detail, "user-1")
    }

    private var viewModelRow: NativeRouteDestinationRow?

    private func loadBrowse(isAdministrator: Bool, users: String) async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users"] = (
            Data("""
            {
              "results": [\(users)],
              "page_info": { "has_next_page": false, "start_cursor": null, "end_cursor": null }
            }
            """.utf8),
            200
        )
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/users?q=alice")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .usersBrowse),
            client: makeClient(),
            routeMatch: match,
            isAdministrator: isAdministrator
        )
        await viewModel.load()
        viewModelRow = viewModel.rows.first
    }
}
