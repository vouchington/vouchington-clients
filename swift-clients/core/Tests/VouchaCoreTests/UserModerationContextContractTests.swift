import Foundation
@testable import VouchaAPI
import VouchaModels
import XCTest

final class UserModerationContextContractTests: XCTestCase {
    func testStaffFixtureRetainsPrivateSignalsAndFilteredNotes() throws {
        let response = try makeVouchaDecoder().decode(
            UserModerationContextResponse.self,
            from: ApiFixtureLoader.data("native.moderation.context.staff")
        )
        XCTAssertEqual(response.context.accountAgeMs, 86_400_000)
        XCTAssertEqual(response.context.trustTier, 1)
        XCTAssertEqual(response.context.contentRemovalCount, 1_000)
        XCTAssertEqual(response.context.contentRemovalCountCapped, true)
        XCTAssertEqual(response.context.communityRemovalCount, 2)
        XCTAssertNil(response.context.activeSuspension)
        XCTAssertFalse(response.pageInfo.hasNextPage)
        let note = try XCTUnwrap(response.notes.first)
        XCTAssertEqual(note.targetUserId, "00000000-0000-7000-8000-000000000911")
        XCTAssertEqual(note.authorUserId, "00000000-0000-7000-8000-000000000913")
        XCTAssertEqual(note.authorUsername, "moderator")
        XCTAssertNil(note.authorDisplayName)
        XCTAssertEqual(note.communityId, "00000000-0000-7000-8000-000000000914")
        XCTAssertEqual(note.communityName, "Fixture community")
        XCTAssertEqual(note.body, "Observed repeated spam.")
        XCTAssertEqual(note.createdAt.timeIntervalSince1970, 1_780_315_200)
        XCTAssertNil(note.deletedAt)
    }

    func testMemberFixtureKeepsStaffSignalsNullAndNotesAvailable() throws {
        let response = try makeVouchaDecoder().decode(
            UserModerationContextResponse.self,
            from: ApiFixtureLoader.data("native.moderation.context.member")
        )
        XCTAssertEqual(response.context.accountAgeMs, 86_400_000)
        XCTAssertNil(response.context.trustTier)
        XCTAssertNil(response.context.activeSuspension)
        XCTAssertNil(response.context.contentRemovalCount)
        XCTAssertNil(response.context.contentRemovalCountCapped)
        XCTAssertNil(response.context.communityRemovalCount)
        XCTAssertEqual(response.notes.map(\.id), ["00000000-0000-7000-8000-000000000912"])
        XCTAssertFalse(response.pageInfo.hasNextPage)
    }

    func testEndpointEncodesUserPathSegment() {
        let endpoint = Endpoint.userModerationContext(userId: "user/name")
        XCTAssertEqual(endpoint.method, .GET)
        XCTAssertEqual(endpoint.path, "/api/v1/users/user%2Fname/moderation-context")
        XCTAssertTrue(endpoint.queryItems.isEmpty)
    }
}
