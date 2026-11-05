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
}
