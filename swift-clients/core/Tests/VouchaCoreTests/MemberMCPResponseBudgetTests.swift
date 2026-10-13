import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAPI
import VouchaModels
import XCTest

extension MemberMCPClientTests {
    func testProducerSizedToolResultSurvivesJSONAndSSETransportLines() async throws {
        let text = String(repeating: "/", count: 1_048_500)
        let result = Data(#"{"content":[{"type":"text","text":""#.utf8) +
            Data(text.utf8) + Data(#""}],"isError":false}"#.utf8)
        XCTAssertLessThanOrEqual(result.count, 1_048_576)
        let rpc = Data(#"{"jsonrpc":"2.0","id":"2","result": "#.utf8) +
            result + Data("}".utf8)
        XCTAssertGreaterThan(rpc.count, 1_048_576)
        XCTAssertLessThanOrEqual(rpc.count + 6, 1_100_000)

        for contentType in ["application/json", "text/event-stream"] {
            let response = contentType == "application/json" ? rpc :
                Data("data: ".utf8) + rpc + Data("\n\n".utf8)
            MCPTestURLProtocol.configure(data: response, status: 200, contentType: contentType)
            let client = try MemberMCPClient(
                siteOrigin: XCTUnwrap(URL(string: "https://example.test")),
                protocolClasses: [MCPTestURLProtocol.self]
            )

            let actual = try await client.callTool(
                name: "read_post", arguments: ["id": .string("post-1")], accessToken: "access-1"
            )

            guard case let .object(first)? = actual.content.first,
                  case let .string(actualText)? = first["text"] else {
                return XCTFail("MCP transport discarded a producer-bounded result")
            }
            XCTAssertEqual(actualText, text)
        }
    }
}
