import Foundation
import VouchaAPI
import VouchaModels
import XCTest

final class MembershipAdministrationModelTests: XCTestCase {
    func testGrantPlanSlugsRemainFiniteAndCodable() throws {
        XCTAssertEqual(MembershipPlanSlug.allCases, [.plus, .pro])
        XCTAssertEqual(MembershipPlanSlug.plus.id, "plus")
        XCTAssertEqual(MembershipPlanSlug.pro.id, "pro")
        XCTAssertEqual(try JSONDecoder().decode(MembershipPlanSlug.self, from: Data(#""pro""#.utf8)), .pro)
    }

    func testDecodesMembershipGrantResponse() throws {
        let response = try JSONDecoder().decode(
            MembershipGrantResponse.self,
            from: Data(#"{"grant":{"id":"grant-1"},"membership":{"id":"membership-1"},"queued":false}"#.utf8)
        )

        XCTAssertEqual(response.grant.id, "grant-1")
        XCTAssertEqual(response.membership.id, "membership-1")
        XCTAssertFalse(response.queued)
    }

    func testGrantSearchUserAcceptsMissingUsernameAndUsesIdentifierFallback() throws {
        let user = try makeVouchaDecoder().decode(
            MembershipGrantUser.self,
            from: Data(#"{"id":"user-1","username":null,"name":"Private user"}"#.utf8)
        )

        XCTAssertNil(user.username)
        XCTAssertEqual(user.displayLabel, "user-1")
    }

    func testGrantSearchUsesTheAdministratorSearchContract() {
        let endpoint = Endpoint.membershipGrantUserSearch(query: "private", limit: 8)

        XCTAssertEqual(endpoint.path, "/api/v1/users")
        XCTAssertEqual(endpoint.queryItems, [
            URLQueryItem(name: "q", value: "private"),
            URLQueryItem(name: "limit", value: "8")
        ])
    }

    func testGrantSearchForwardsTheContinuationCursor() {
        let endpoint = Endpoint.membershipGrantUserSearch(query: "private", after: "cursor-1", limit: 8)

        XCTAssertEqual(endpoint.queryItems, [
            URLQueryItem(name: "q", value: "private"),
            URLQueryItem(name: "after", value: "cursor-1"),
            URLQueryItem(name: "limit", value: "8")
        ])
    }
}
