import Foundation
import XCTest

final class DirectMessagesTestJSONTests: XCTestCase {
    func testHelpersKeepSnakeCaseISO8601AndJSONQuoting() throws {
        struct Sample: Decodable {
            let createdAt: Date
            let bodyText: String
        }

        let decoded: Sample = try DirectMessagesTestJSON.decode(
            #"{"created_at":"2026-01-01T00:00:00Z","body_text":"hello"}"#
        )
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = try XCTUnwrap(TimeZone(secondsFromGMT: 0))
        let components = calendar.dateComponents(
            [.year, .month, .day, .hour, .minute, .second],
            from: decoded.createdAt
        )

        XCTAssertEqual(decoded.bodyText, "hello")
        XCTAssertEqual(components.year, 2_026)
        XCTAssertEqual(components.month, 1)
        XCTAssertEqual(components.day, 1)
        XCTAssertEqual(components.hour, 0)
        XCTAssertEqual(components.minute, 0)
        XCTAssertEqual(components.second, 0)
        XCTAssertEqual(decoded.createdAt, DirectMessagesTestJSON.date("2026-01-01T00:00:00Z"))
        XCTAssertEqual(DirectMessagesTestJSON.array(["a", "b"]), "[\"a\",\"b\"]")
        XCTAssertEqual(DirectMessagesTestJSON.string(nil), "null")
        XCTAssertEqual(DirectMessagesTestJSON.string("a"), "\"a\"")
    }

    func testOwnerParticipantsPageCarriesTheRequestedOwner() throws {
        struct Page: Decodable {
            struct Participant: Decodable {
                let id: String
                let conversationId: String
                let userId: String
                let role: String
                let username: String
                let profileImageId: String?
                let createdAt: Date
                let removedAt: Date?
            }

            struct PageInfo: Decodable {
                let hasNextPage: Bool
                let endCursor: String?
                let startCursor: String?
            }

            let results: [Participant]
            let pageInfo: PageInfo
        }

        let page: Page = try DirectMessagesTestJSON.decode(
            String(
                decoding: DirectMessagesTestJSON.ownerParticipantsPage(
                    conversationId: "conversation-9",
                    ownerUserId: "user-9"
                ),
                as: UTF8.self
            )
        )
        let owner = try XCTUnwrap(page.results.first)

        XCTAssertEqual(page.results.count, 1)
        XCTAssertEqual(owner.id, "participant-owner")
        XCTAssertEqual(owner.conversationId, "conversation-9")
        XCTAssertEqual(owner.userId, "user-9")
        XCTAssertEqual(owner.role, "owner")
        XCTAssertEqual(owner.username, "alice")
        XCTAssertNil(owner.profileImageId)
        XCTAssertEqual(owner.createdAt, DirectMessagesTestJSON.date("2026-01-01T00:00:00Z"))
        XCTAssertNil(owner.removedAt)
        XCTAssertFalse(page.pageInfo.hasNextPage)
        XCTAssertNil(page.pageInfo.endCursor)
        XCTAssertNil(page.pageInfo.startCursor)
    }
}
