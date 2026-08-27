import Foundation
import VouchaModels
import XCTest

final class UserSearchResultTests: XCTestCase {
    func testPublicPayloadLeavesAdminFieldsNil() throws {
        let user = try makeVouchaDecoder().decode(
            UserSearchResult.self,
            from: Data(#"{"id":"user-1","username":"alice"}"#.utf8)
        )

        XCTAssertEqual(user.id, "user-1")
        XCTAssertEqual(user.username, "alice")
        XCTAssertNil(user.emailAddress)
        XCTAssertNil(user.suspendedAt)
        XCTAssertNil(user.suspendedReason)
        XCTAssertNil(user.suspendedById)
    }

    func testAdminPayloadPopulatesEmailAndSuspension() throws {
        let user = try makeVouchaDecoder().decode(
            UserSearchResult.self,
            from: Data(
                """
                {
                  "id": "user-1",
                  "username": "alice",
                  "email_address": "alice@example.com",
                  "suspended_at": "2026-01-02T03:04:05Z",
                  "suspended_reason": "spam",
                  "suspended_by_id": "admin-1"
                }
                """.utf8
            )
        )

        XCTAssertEqual(user.emailAddress, "alice@example.com")
        XCTAssertEqual(
            user.suspendedAt.map { ISO8601DateFormatter().string(from: $0) },
            "2026-01-02T03:04:05Z"
        )
        XCTAssertEqual(user.suspendedReason, "spam")
        XCTAssertEqual(user.suspendedById, "admin-1")
    }

    func testMissingUsernameSucceeds() throws {
        let user = try makeVouchaDecoder().decode(
            UserSearchResult.self,
            from: Data(#"{"id":"user-1"}"#.utf8)
        )

        XCTAssertEqual(user.id, "user-1")
        XCTAssertNil(user.username)
    }

    func testExtraPrivateViewKeysAreIgnored() throws {
        let user = try makeVouchaDecoder().decode(
            UserSearchResult.self,
            from: Data(
                """
                {
                  "id": "user-1",
                  "username": "alice",
                  "email_address": "alice@example.com",
                  "membership_plan": "pro",
                  "cards_visibility": "everyone"
                }
                """.utf8
            )
        )

        XCTAssertEqual(user.emailAddress, "alice@example.com")
        XCTAssertEqual(user.username, "alice")
    }

    func testPageRequiresPageInfo() throws {
        XCTAssertThrowsError(
            try makeVouchaDecoder().decode(
                Page<UserSearchResult>.self,
                from: Data(#"{"results":[{"id":"user-1"}]}"#.utf8)
            )
        )
    }

    func testPageDecodesResultsWithPageInfo() throws {
        let page = try makeVouchaDecoder().decode(
            Page<UserSearchResult>.self,
            from: Data(
                """
                {
                  "results": [{"id":"user-1","username":"alice"}],
                  "page_info": {
                    "has_next_page": false,
                    "start_cursor": null,
                    "end_cursor": null
                  }
                }
                """.utf8
            )
        )

        XCTAssertEqual(page.results.map(\.id), ["user-1"])
        XCTAssertEqual(page.results.map(\.username), ["alice"])
        XCTAssertFalse(page.pageInfo.hasNextPage)
    }
}
