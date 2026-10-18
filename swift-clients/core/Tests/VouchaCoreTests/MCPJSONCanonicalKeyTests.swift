import Foundation
@testable import VouchaAPI
import VouchaModels
import XCTest

final class MCPJSONCanonicalKeyTests: XCTestCase {
    func testResponseAndExactWriterPreserveCanonicallyEquivalentKeys() async throws {
        let body = #"{"jsonrpc":"2.0","id":"1","result":{"é":1,"e\u0301":2,"K":3,"K":4}}"#
        let value = try await read(body, contentType: "application/json")
        guard case let .object(object) = value else { return XCTFail("Expected result object") }

        let composed = "é"
        let decomposed = "e\u{301}"
        let names = Set(object.keys.map { Data($0.utf8) })
        XCTAssertEqual(names, Set([composed, decomposed, "K", "K"].map { Data($0.utf8) }))
        XCTAssertEqual(number(object[composed]), 1)
        XCTAssertEqual(number(object[decomposed]), 2)
        XCTAssertEqual(number(object["K"]), 3)
        XCTAssertEqual(number(object["K"]), 4)

        let encoded = try value.exactJSONData()
        let reparsed = try DecodedJSONValue(exactJSONData: encoded)
        guard case let .object(reparsedObject) = reparsed else { return XCTFail("Expected re-encoded object") }
        XCTAssertEqual(Set(reparsedObject.keys.map { Data($0.utf8) }), names)
        XCTAssertEqual(number(reparsedObject[composed]), 1)
        XCTAssertEqual(number(reparsedObject[decomposed]), 2)
        XCTAssertEqual(number(reparsedObject["K"]), 3)
        XCTAssertEqual(number(reparsedObject["K"]), 4)
        XCTAssertThrowsError(try JSONEncoder().encode(value))
    }

    func testSSEResponsePreservesExactKeysAndRejectsDecodedDuplicate() async throws {
        let body = #"data: {"jsonrpc":"2.0","id":"1","result":{"é":1,"e\u0301":2,"K":3,"K":4}}"#
        let value = try await read(body + "\n\n", contentType: "text/event-stream")
        guard case let .object(object) = value else { return XCTFail("Expected result object") }
        XCTAssertEqual(object.count, 4)

        do {
            _ = try await read(
                #"{"jsonrpc":"2.0","id":"1","result":{"a":1,"\u0061":2}}"#,
                contentType: "application/json"
            )
            XCTFail("Decoded-identical JSON member names must be rejected")
        } catch MemberMCPClient.Failure.invalidResponse {
            // Duplicate identity is checked before an object can be consumed.
        }
    }

    private func read(_ body: String, contentType: String) async throws -> DecodedJSONValue {
        let lines = AsyncThrowingStream<String, Error> { continuation in
            for line in body.split(separator: "\n", omittingEmptySubsequences: false) {
                continuation.yield(String(line))
            }
            continuation.finish()
        }
        return try await MCPResponseReader.read(
            lines, contentType: contentType, id: "1", sendServerReply: { _ in }
        )
    }

    private func number(_ value: DecodedJSONValue?) -> Double? {
        guard case let .number(number)? = value else { return nil }
        return number
    }
}
