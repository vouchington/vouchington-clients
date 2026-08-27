import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class DirectMessagesViewModelSearchConcurrencyTests: NativeRouteSurfaceViewModelTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
    }

    func testStaleUserSearchCompletionDoesNotReplaceLatestResults() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/users"] = [
            (userSearchPage(id: "user-1", username: "alice"), 200, 0.2),
            (userSearchPage(id: "user-2", username: "bob"), 200, 0)
        ]
        let viewModel = try DirectMessagesViewModel(client: makeClient())

        let staleSearch = Task { await viewModel.searchUsers(query: "alice") }
        try await waitForCapturedUserSearch(count: 1)
        await viewModel.searchUsers(query: "bob")
        await staleSearch.value

        XCTAssertEqual(viewModel.userResults.map(\.id), ["user-2"])
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.compactMap { url in
                URLComponents(url: url, resolvingAgainstBaseURL: false)?
                    .queryItems?
                    .first(where: { $0.name == "q" })?
                    .value
            },
            ["alice", "bob"]
        )
    }

    func testClearingUserSearchSuppressesPendingResults() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/users"] = [
            (userSearchPage(id: "user-1", username: "alice"), 200, 0.2)
        ]
        let viewModel = try DirectMessagesViewModel(client: makeClient())

        let staleSearch = Task { await viewModel.searchUsers(query: "alice") }
        try await waitForCapturedUserSearch(count: 1)
        await viewModel.searchUsers(query: "   ")
        await staleSearch.value

        XCTAssertTrue(viewModel.userResults.isEmpty)
    }

    func testClearingParticipantSearchSuppressesPendingResults() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/users"] = [
            (userSearchPage(id: "user-1", username: "alice"), 200, 0.2)
        ]
        let viewModel = try DirectMessagesViewModel(client: makeClient())

        let staleSearch = Task { await viewModel.searchParticipantUsers(query: "alice") }
        try await waitForCapturedUserSearch(count: 1)
        viewModel.clearParticipantSearch()
        await staleSearch.value

        XCTAssertTrue(viewModel.participantUserResults.isEmpty)
    }

    func testComposerAndParticipantSearchResultsStaySeparate() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/users"] = [
            (userSearchPage(id: "user-1", username: "alice"), 200, 0),
            (userSearchPage(id: "user-2", username: "bob"), 200, 0)
        ]
        let viewModel = try DirectMessagesViewModel(client: makeClient())

        await viewModel.searchUsers(query: "alice")
        await viewModel.searchParticipantUsers(query: "bob")

        XCTAssertEqual(viewModel.userResults.map(\.id), ["user-1"])
        XCTAssertEqual(viewModel.participantUserResults.map(\.id), ["user-2"])
    }

    func testSearchFailuresClearResultsWithoutChangingInboxState() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/users"] = [
            (userSearchPage(id: "user-1", username: "alice"), 200, 0),
            (Data("{}".utf8), 500, 0),
            (userSearchPage(id: "user-2", username: "bob"), 200, 0),
            (Data("{}".utf8), 500, 0)
        ]
        let viewModel = try DirectMessagesViewModel(client: makeClient())
        viewModel.state = .loaded

        await viewModel.searchUsers(query: "alice")
        XCTAssertEqual(viewModel.userResults.map(\.id), ["user-1"])
        await viewModel.searchUsers(query: "alice")
        XCTAssertTrue(viewModel.userResults.isEmpty)
        switch viewModel.state {
        case .loaded:
            break
        default:
            XCTFail("Expected inbox state to remain loaded")
        }

        await viewModel.searchParticipantUsers(query: "bob")
        XCTAssertEqual(viewModel.participantUserResults.map(\.id), ["user-2"])
        await viewModel.searchParticipantUsers(query: "bob")
        XCTAssertTrue(viewModel.participantUserResults.isEmpty)
        switch viewModel.state {
        case .loaded:
            break
        default:
            XCTFail("Expected inbox state to remain loaded")
        }
    }

    func testUserSearchExcludesSelectedRecipientsAndFollowsCursorToFillPage() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/users"] = [
            (userSearchPage(id: "user-1", username: "alice", cursor: "next", hasMore: true), 200, 0),
            (userSearchPage(id: "user-2", username: "bob"), 200, 0)
        ]
        let viewModel = try DirectMessagesViewModel(client: makeClient())

        await viewModel.searchUsers(query: "a", excludeIds: ["user-1"])

        XCTAssertEqual(viewModel.userResults.map(\.id), ["user-2"])
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/users" }.map { url in
                URLComponents(url: url, resolvingAgainstBaseURL: false)?
                    .queryItems?
                    .first(where: { $0.name == "after" })?
                    .value
            },
            [nil, "next"]
        )
    }

    func testParticipantSearchFollowsCursorToFillPage() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/users"] = [
            (userSearchPage(id: "user-1", username: "alice", cursor: "next", hasMore: true), 200, 0),
            (userSearchPage(id: "user-2", username: "bob"), 200, 0)
        ]
        let viewModel = try DirectMessagesViewModel(client: makeClient())

        await viewModel.searchParticipantUsers(query: "a")

        XCTAssertEqual(viewModel.participantUserResults.map(\.id), ["user-1", "user-2"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/users" }.count, 2)
    }

    func testUserSearchStopsFollowingCursorAtSafetyBound() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/users"] = (0 ..< 5).map { page in
            (userSearchPage(id: "excluded", username: "alice", cursor: "cursor-\(page)", hasMore: true), 200, 0)
        }
        let viewModel = try DirectMessagesViewModel(client: makeClient())

        await viewModel.searchUsers(query: "a", excludeIds: ["excluded"])

        XCTAssertTrue(viewModel.userResults.isEmpty)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/users" }.count, 5)
    }

    private func userSearchPage(
        id: String,
        username: String,
        cursor: String? = nil,
        hasMore: Bool = false
    ) -> Data {
        let endCursor = cursor.map { "\"\($0)\"" } ?? "null"
        return Data(
            """
            {
              "results": [
                {
                  "id": "\(id)",
                  "username": "\(username)",
                  "roles": [],
                  "profile_image_id": null,
                  "markdown": null
                }
              ],
              "page_info": {
                "has_next_page": \(hasMore),
                "end_cursor": \(endCursor),
                "start_cursor": null
              }
            }
            """.utf8
        )
    }

    private func waitForCapturedUserSearch(count: Int) async throws {
        for _ in 0 ..< 40 {
            if CannedFeedURLProtocol.capturedURLs.filter({ $0.path == "/api/v1/users" }).count >= count {
                return
            }
            try await Task.sleep(nanoseconds: 10_000_000)
        }
        XCTFail("Timed out waiting for user search request.")
    }
}
