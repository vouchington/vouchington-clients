import Foundation
@testable import VouchaAPI
import VouchaModels
import XCTest

final class MCPJSONCanonicalKeyTests: XCTestCase {
    func testResponseRejectsDistinctCanonicalEquivalentObjectKeys() async throws {
        let body = #"{"jsonrpc":"2.0","id":"1","result":{"é":1,"e\u0301":2}}"#
        let lines = AsyncThrowingStream<String, Error> { continuation in
            continuation.yield(body)
            continuation.finish()
        }

        do {
            let _: DecodedJSONValue = try await MCPResponseReader.read(
                lines,
                contentType: "application/json",
                id: "1",
                sendServerReply: { _ in }
            )
            XCTFail("MCP JSON with keys that collapse in Swift dictionaries must be rejected")
        } catch MemberMCPClient.Failure.invalidResponse {
            // The raw JSON names are distinct, but Swift's dictionary key equality is not.
        }
    }
}
