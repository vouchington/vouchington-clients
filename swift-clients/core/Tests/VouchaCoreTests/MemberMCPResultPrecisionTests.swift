import Foundation
@testable import VouchaAPI
import VouchaModels
import XCTest

final class MemberMCPResultPrecisionTests: XCTestCase {
    func testStructuredResultPreservesLargeIntegerPrecision() throws {
        let json = Data(#"{"content":[],"structuredContent":{"id":9007199254740993}}"#.utf8)
        let result = try JSONDecoder().decode(MemberMCPClient.ToolResult.self, from: json)
        guard case let .object(fields)? = result.structuredContent,
              case let .integer(id)? = fields["id"] else {
            return XCTFail("Large MCP integer lost its integer representation")
        }
        XCTAssertEqual(id, 9_007_199_254_740_993)
        let encoded = try JSONEncoder().encode(result.structuredContent)
        XCTAssertTrue(String(decoding: encoded, as: UTF8.self).contains("9007199254740993"))
    }

    func testStructuredResultPreservesUnsignedAndLargeDecimalIntegers() throws {
        let json = Data(
            #"{"content":[],"structuredContent":{"unsigned":18446744073709551615,"large":123456789012345678901234567890}}"#
                .utf8
        )
        let result = try JSONDecoder().decode(MemberMCPClient.ToolResult.self, from: json)
        guard case let .object(fields)? = result.structuredContent,
              case let .unsignedInteger(unsigned)? = fields["unsigned"],
              case .decimal? = fields["large"] else {
            return XCTFail("MCP integers outside signed 64-bit range must remain exact")
        }
        XCTAssertEqual(unsigned, UInt64.max)
        let encoded = try JSONEncoder().encode(result.structuredContent)
        let wire = String(decoding: encoded, as: UTF8.self)
        XCTAssertTrue(wire.contains("18446744073709551615"))
        XCTAssertTrue(wire.contains("123456789012345678901234567890"))
    }
}
