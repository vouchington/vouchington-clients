import Foundation
import VouchaTestSupport
import XCTest

final class FinancialMcpManifestContractTests: XCTestCase {
    func testPublishedToolsSeparateFinancialReadsFromTheGeneralProfile() throws {
        let root = try FilamentsContractRoot.url(requiredPaths: ["api-fixtures/v1/mcp.json"])
        let url = try FilamentsContractRoot.requiredURL("api-fixtures/v1/mcp.json", root: root)
        let manifest = try object(JSONSerialization.jsonObject(with: Data(contentsOf: url)))
        let servers = try XCTUnwrap(manifest["servers"] as? [[String: Any]])
        let tools = try XCTUnwrap(servers.first?["tools"] as? [[String: Any]])
        let general = try tool("get_my_profile", in: tools)
        let financial = try tool("get_my_financial_profile", in: tools)

        XCTAssertEqual(try requiredScopes(general), ["profile:read"])
        XCTAssertEqual(try requiredScopes(financial), ["financial-profile:read"])
        let generalOutput = try object(general["outputSchema"])
        XCTAssertFalse(containsKey("financial_profile", in: generalOutput))
        XCTAssertFalse(containsKey("credit_score_range", in: generalOutput))

        let output = try object(financial["outputSchema"])
        let result = try object(object(output["properties"])["result"])
        XCTAssertTrue((result["required"] as? [String])?.contains("financial_profile") == true)
        let profile = try object(object(result["properties"])["financial_profile"])
        let branches = try XCTUnwrap(profile["anyOf"] as? [[String: Any]])
        XCTAssertTrue(branches.contains { $0["type"] as? String == "null" })
        XCTAssertTrue(branches.contains {
            $0["type"] as? String == "object" &&
                (($0["properties"] as? [String: Any])?["credit_score_range"] != nil)
        })
    }

    private func tool(_ name: String, in entries: [[String: Any]]) throws -> [String: Any] {
        let matches = entries.compactMap { $0["tool"] as? [String: Any] }
            .filter { $0["name"] as? String == name }
        XCTAssertEqual(matches.count, 1)
        return try XCTUnwrap(matches.first)
    }

    private func requiredScopes(_ tool: [String: Any]) throws -> [String] {
        try XCTUnwrap(object(tool["_meta"])["voucha/requiredScopes"] as? [String])
    }

    private func object(_ value: Any?) throws -> [String: Any] {
        try XCTUnwrap(value as? [String: Any])
    }

    private func containsKey(_ key: String, in value: Any) -> Bool {
        if let object = value as? [String: Any] {
            return object[key] != nil || object.values.contains { containsKey(key, in: $0) }
        }
        if let array = value as? [Any] {
            return array.contains { containsKey(key, in: $0) }
        }
        return false
    }
}
