@testable import VouchaCore
import XCTest

final class UuidV7Tests: XCTestCase {
    func testStringEmbedsTheTimestampAndVersionBits() throws {
        let first = UuidV7.string(milliseconds: 0x0123_4567_89AB)
        let second = UuidV7.string(milliseconds: 0x0123_4567_89AB)

        try assertUuidV7(first, milliseconds: 0x0123_4567_89AB)
        try assertUuidV7(second, milliseconds: 0x0123_4567_89AB)
        XCTAssertNotEqual(first, second)
        XCTAssertEqual(first.prefix(14), "01234567-89ab-")
    }

    func testSequentialPairUsesTheSuppliedInstantAndTheNextMillisecond() throws {
        let pair = UuidV7.nextSequentialPair(at: Date(timeIntervalSince1970: 1_700_000_000))

        try assertUuidV7(pair.0, milliseconds: 1_700_000_000_000)
        try assertUuidV7(pair.1, milliseconds: 1_700_000_000_001)
        XCTAssertLessThan(pair.0, pair.1)
    }

    func testSequentialPairFromTheCurrentClockIsOrderedByOneMillisecond() throws {
        let pair = UuidV7.nextSequentialPair()
        let pattern = #"^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$"#

        XCTAssertNotNil(pair.0.range(of: pattern, options: .regularExpression))
        XCTAssertNotNil(pair.1.range(of: pattern, options: .regularExpression))
        XCTAssertEqual(try unixMilliseconds(in: pair.1), try unixMilliseconds(in: pair.0) + 1)
        XCTAssertLessThan(pair.0, pair.1)
    }

    private func assertUuidV7(_ uuid: String, milliseconds: UInt64) throws {
        let pattern = #"^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$"#
        XCTAssertNotNil(
            uuid.range(of: pattern, options: .regularExpression),
            "expected a lowercase UUID v7, got \(uuid)"
        )
        XCTAssertEqual(try unixMilliseconds(in: uuid), milliseconds)
    }

    private func unixMilliseconds(in uuid: String) throws -> UInt64 {
        let hex = uuid.replacingOccurrences(of: "-", with: "")
        return try XCTUnwrap(UInt64(hex.prefix(12), radix: 16))
    }
}
