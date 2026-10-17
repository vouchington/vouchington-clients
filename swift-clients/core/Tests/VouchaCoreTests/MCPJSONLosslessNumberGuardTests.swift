import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAPI
import VouchaModels
import XCTest

final class MCPJSONLosslessNumberGuardTests: XCTestCase {
    override func tearDown() {
        MCPTestURLProtocol.configure(data: Data(), status: 200)
        super.tearDown()
    }

    func testRawToolResponseRejectsRoundedSubnormalAndUnderflowWithoutRedispatch() async throws {
        for number in ["4e-324", "7e-324", "1e-400", "-4e-324"] {
            let client = try client(number: number)
            do {
                _ = try await client.callTool(name: "read_fixture", arguments: [:], accessToken: "access-1")
                XCTFail("A validated result must not silently change numeric value: \(number)")
            } catch MemberMCPClient.Failure.invalidResponse {}
            XCTAssertEqual(MCPTestURLProtocol.methodHistory().filter { $0 == "tools/call" }.count, 1)
        }
    }

    func testRawToolResponsePreservesSupportedScientificAndLargeIntegerRepresentations() async throws {
        for number in [
            "5e-324", "1e300", "1e-300", "9007199254740993", "18446744073709551615",
            "123456789012345678901234567890", "0.099999999999999999", "-0.0"
        ] {
            let client = try client(number: number)
            let result = try await client.callTool(name: "read_fixture", arguments: [:], accessToken: "access-1")
            let original = try JSONDecoder().decode(DecodedJSONValue.self, from: Data(number.utf8))
            guard case let .object(fields)? = result.structuredContent, let retained = fields["value"] else {
                return XCTFail("The complete structured value must survive")
            }
            XCTAssertEqual(try JSONEncoder().encode(retained), try JSONEncoder().encode(original))
            XCTAssertEqual(MCPTestURLProtocol.methodHistory().filter { $0 == "tools/call" }.count, 1)
        }
    }

    func testQuotedNumbersAreContentAndHugeNonzeroExponentsFailClosed() throws {
        let content = try JSONEncoder().encode(["text": "4e-324 \"1e-400\""])
        XCTAssertNoThrow(try MCPJSONLosslessNumberGuard.validate(content))
        XCTAssertThrowsError(try MCPJSONLosslessNumberGuard
            .validate(Data(("1e" + String(repeating: "9", count: 1_000)).utf8)))
        XCTAssertNoThrow(try MCPJSONLosslessNumberGuard
            .validate(Data(("0e" + String(repeating: "9", count: 1_000)).utf8)))
    }

    private func client(number: String) throws -> MemberMCPClient {
        let response = "{\"jsonrpc\":\"2.0\",\"id\":\"2\",\"result\":{\"content\":[],\"structuredContent\":{\"value\":\(number)}}}"
        MCPTestURLProtocol.configure(data: Data(response.utf8), status: 200)
        return try MemberMCPClient(
            siteOrigin: XCTUnwrap(URL(string: "https://example.test")), protocolClasses: [MCPTestURLProtocol.self]
        )
    }
}
