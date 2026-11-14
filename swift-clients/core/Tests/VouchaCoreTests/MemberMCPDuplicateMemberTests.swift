import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAPI
import VouchaModels
import XCTest

extension MemberMCPClientTests {
    func testJSONAndSSERejectDuplicateResultMemberNames() async throws {
        let objects = [
            #"{"status":"approved","status":"denied"}"#,
            #"{"status":"approved","\u0073tatus":"denied"}"#,
            #"{"😀":1,"\ud83d\ude00":2}"#
        ]
        for object in objects {
            for type in ["application/json", "text/event-stream"] {
                let rpc = "{\"jsonrpc\":\"2.0\",\"id\":\"2\",\"result\":{" +
                    "\"content\":[],\"structuredContent\":\(object)}}"
                let reply = type == "application/json" ? rpc : "data: \(rpc)\n\n"
                MCPTestURLProtocol.configure(data: Data(reply.utf8), status: 200, contentType: type)
                let client = try MemberMCPClient(
                    siteOrigin: XCTUnwrap(URL(string: "https://example.test")),
                    protocolClasses: [MCPTestURLProtocol.self]
                )
                do {
                    _ = try await client.callTool(name: "read_my_profile", arguments: [:], accessToken: "access-1")
                    XCTFail("Accepted ambiguous MCP result")
                } catch MemberMCPClient.Failure.invalidResponse {} catch { XCTFail("Unexpected error: \(error)") }
            }
        }
    }

    func testDistinctUnicodeKeysAndOrdinaryNestedResultsRemainValid() async throws {
        let rpc = #"""
        {"jsonrpc":"2.0","id":"2","result":{"content":[],"structuredContent":{
        "é":1,"ê":2,"nested":[{"status":"approved"},{"status":"denied"}]}}}
        """#.replacingOccurrences(of: "\n", with: "")
        for type in ["application/json", "text/event-stream"] {
            let reply = type == "application/json" ? rpc : "data: \(rpc)\n\n"
            MCPTestURLProtocol.configure(data: Data(reply.utf8), status: 200, contentType: type)
            let client = try MemberMCPClient(
                siteOrigin: XCTUnwrap(URL(string: "https://example.test")),
                protocolClasses: [MCPTestURLProtocol.self]
            )
            let result = try await client.callTool(name: "read_my_profile", arguments: [:], accessToken: "access-1")
            guard case let .object(fields)? = result.structuredContent,
                  case let .number(acute)? = fields["é"], case let .number(circumflex)? = fields["ê"] else {
                return XCTFail("Both distinct Unicode fields must survive the full MCP response")
            }
            XCTAssertEqual(acute, 1)
            XCTAssertEqual(circumflex, 2)
        }
    }

    func testDuplicateServerRequestDoesNotSendAReply() async throws {
        let events = #"data: {"jsonrpc":"2.0","id":17,"method":"ping","method":"other"}"# + "\n\n"
        MCPTestURLProtocol.configure(data: Data(events.utf8), status: 200, contentType: "text/event-stream")
        let client = try MemberMCPClient(
            siteOrigin: XCTUnwrap(URL(string: "https://example.test")),
            protocolClasses: [MCPTestURLProtocol.self]
        )
        do {
            _ = try await client.listTools(accessToken: "access-1")
            XCTFail("Accepted ambiguous server request")
        } catch MemberMCPClient.Failure.invalidResponse {} catch { XCTFail("Unexpected error: \(error)") }
        XCTAssertNil(MCPTestURLProtocol.serverReply())
    }
}
