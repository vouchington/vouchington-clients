import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
extension MembershipGrantViewModelTests {
    func testClearingSearchSuppressesDelayedCandidates() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/users"] = [(userSearchPage, 200, 0.2)]
        let viewModel = try MembershipGrantViewModel(client: makeClient())
        viewModel.query = "alice"

        let search = Task { await viewModel.search() }
        try await waitForMembershipSearch(path: "/api/v1/users")
        viewModel.updateQuery("")
        await search.value

        XCTAssertNil(viewModel.selectedUser)
        XCTAssertTrue(viewModel.candidates.isEmpty)
        XCTAssertFalse(viewModel.isSearching)
    }

    func testSelectingUserSuppressesDelayedCandidates() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/users"] = [(userSearchPage, 200, 0.2)]
        let viewModel = try MembershipGrantViewModel(client: makeClient())
        let selected = try user()
        viewModel.query = "alice"
        let search = Task { await viewModel.search() }
        try await waitForMembershipSearch(path: "/api/v1/users")
        viewModel.selectUser(selected)
        await search.value

        XCTAssertEqual(viewModel.selectedUser?.id, selected.id)
        XCTAssertTrue(viewModel.candidates.isEmpty)
        XCTAssertFalse(viewModel.isSearching)
    }

    func testSearchPublishesCandidates() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/users"] = [(userSearchPage, 200, 0)]
        let viewModel = try MembershipGrantViewModel(client: makeClient())
        viewModel.query = "alice"

        await viewModel.search()

        XCTAssertEqual(viewModel.candidates.map(\.displayLabel), ["@alice"])
        XCTAssertNil(viewModel.searchError)
    }

    func testFailedSearchPublishesError() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/users"] = [(Data("{}".utf8), 500, 0)]
        let viewModel = try MembershipGrantViewModel(client: makeClient())
        viewModel.query = "alice"

        await viewModel.search()

        XCTAssertTrue(viewModel.candidates.isEmpty)
        XCTAssertNotNil(viewModel.searchError)
    }

    func testSearchAcceptsUserWithoutUsername() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/users"] = [(
            Data(
                #"""
                {"results":[{"id":"user-without-username","username":null}],
                "page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}
                """#
                .utf8
            ),
            200,
            0
        )]
        let viewModel = try MembershipGrantViewModel(client: makeClient())
        viewModel.query = "private"

        await viewModel.search()

        XCTAssertEqual(viewModel.candidates.first?.displayLabel, "user-without-username")
    }

    func testSelectingUsernameNullCandidateGrantsItsIdentifier() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/users"] = [(
            nullUsernameSearchPage,
            200,
            0
        )]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/memberships/plans"] = [
            (grantPlansResponse, 200, 0),
            (grantPlansResponse, 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/membership-grants"] = [(
            Data(#"{"grant":{"id":"grant-1"},"membership":{"id":"membership-1"},"queued":false}"#.utf8),
            201,
            0
        )]
        let viewModel = try MembershipGrantViewModel(client: makeClient())
        viewModel.query = "private"

        await viewModel.search()
        try viewModel.selectUser(XCTUnwrap(viewModel.candidates.first))
        await viewModel.loadPlans()
        viewModel.selectPlan(.plus)
        viewModel.selectedSkuId = "sku-plus"
        viewModel.durationDays = "30"

        await viewModel.grant()
        let body = try XCTUnwrap(CannedFeedURLProtocol.capturedBodies.last.flatMap { $0 })
        XCTAssertTrue(body.contains("\"user_id\":\"user-without-username\""))
    }

    func testSearchFollowsCursorToFillPage() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/users"] = [
            (membershipUserSearchPage(id: "user-1", username: "alice", cursor: "next", hasMore: true), 200, 0),
            (membershipUserSearchPage(id: "user-2", username: "alicia"), 200, 0)
        ]
        let viewModel = try MembershipGrantViewModel(client: makeClient())
        viewModel.query = "ali"

        await viewModel.search()

        XCTAssertEqual(viewModel.candidates.map(\.id), ["user-1", "user-2"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/users" }.count, 2)
        XCTAssertNil(viewModel.searchError)
    }

    func testSearchStopsFollowingCursorAtSafetyBound() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/users"] = (0 ..< 5).map { page in
            (emptyMembershipUserSearchPage(cursor: "c-\(page)"), 200, 0)
        }
        let viewModel = try MembershipGrantViewModel(client: makeClient())
        viewModel.query = "ali"

        await viewModel.search()

        XCTAssertTrue(viewModel.candidates.isEmpty)
        XCTAssertNil(viewModel.searchError)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/users" }.count, 5)
    }

    private func membershipUserSearchPage(
        id: String,
        username: String,
        cursor: String? = nil,
        hasMore: Bool = false
    ) -> Data {
        let endCursor = cursor.map { "\"\($0)\"" } ?? "null"
        return Data(
            """
            {"results":[{"id":"\(id)","username":"\(username)","roles":[],"profile_image_id":null,\
            "markdown":null}],"page_info":{"has_next_page":\(hasMore),"start_cursor":null,\
            "end_cursor":\(endCursor)}}
            """.utf8
        )
    }

    private func emptyMembershipUserSearchPage(cursor: String) -> Data {
        Data(
            """
            {"results":[],"page_info":{"has_next_page":true,"start_cursor":null,"end_cursor":"\(cursor)"}}
            """.utf8
        )
    }

    private func waitForMembershipSearch(path: String) async throws {
        for _ in 0 ..< 40 {
            if CannedFeedURLProtocol.capturedURLs.contains(where: { $0.path == path }) {
                return
            }
            try await Task.sleep(nanoseconds: 10_000_000)
        }
        XCTFail("Expected a request to \(path)")
    }

    private var nullUsernameSearchPage: Data {
        Data(
            #"""
            {"results":[{"id":"user-without-username","username":null}],
            "page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}
            """#
            .utf8
        )
    }

    private var grantPlansResponse: Data {
        Data(
            #"""
            {"products":[{"id":"sku-plus","plan":"plus","interval":"monthly","providers":[{"provider":"stripe",
            "environment":"test","application_id":"voucha-web","product_id":"price-plus","base_plan_id":null,
            "offer_id":null,"sku_id":null,"price":{"amount":500,"currency":"usd"}}]}]}
            """#
            .utf8
        )
    }
}
