import Foundation
import VouchaAPI
import VouchaCore
import VouchaModels
import VouchaTestSupport
import XCTest

struct NativeChatMCPProfileFixture {
    enum Option: String { case bio, links }

    let catalog: DecodedJSONValue
    let arguments: String
    var structured: [String: Any]

    init(option: Option) throws {
        let root = try FilamentsContractRoot.url(requiredPaths: [
            "api-fixtures/v1/mcp.json", "api-fixtures/v1/mcp-results.json"
        ])
        func object(_ path: String) throws -> [String: Any] {
            try XCTUnwrap(JSONSerialization.jsonObject(with: Data(contentsOf: root.appendingPathComponent(path)))
                as? [String: Any])
        }
        let servers = try XCTUnwrap(object("api-fixtures/v1/mcp.json")["servers"] as? [[String: Any]])
        let server = try XCTUnwrap(servers.first { $0["name"] as? String == "voucha-user-mcp" })
        let entries = try XCTUnwrap(server["tools"] as? [[String: Any]])
        let tool = try XCTUnwrap(entries.compactMap { $0["tool"] as? [String: Any] }
            .first { $0["name"] as? String == "read_my_profile" })
        let rows = try XCTUnwrap(object("api-fixtures/v1/mcp-results.json")["cases"] as? [[String: Any]])
        let row = try XCTUnwrap(rows.first { $0["id"] as? String == "native.mcp.read-my-profile.\(option.rawValue)" })
        structured = try XCTUnwrap(row["structuredContent"] as? [String: Any])
        arguments = try XCTUnwrap(String(
            data: JSONSerialization.data(withJSONObject: XCTUnwrap(row["arguments"])), encoding: .utf8
        ))
        catalog = try JSONDecoder().decode(DecodedJSONValue.self, from: JSONSerialization.data(
            withJSONObject: ["tools": [tool]]
        ))
    }

    var value: DecodedJSONValue {
        get throws { try DecodedJSONValue(exactJSONData: JSONSerialization.data(withJSONObject: structured)) }
    }

    func result(text: String = "synthetic complete-envelope marker", isError: Bool = false) throws -> MemberMCPClient
        .ToolResult {
        try JSONDecoder().decode(MemberMCPClient.ToolResult.self, from: JSONSerialization.data(withJSONObject: [
            "content": [["type": "text", "text": text]], "isError": isError, "structuredContent": structured
        ]))
    }
}
