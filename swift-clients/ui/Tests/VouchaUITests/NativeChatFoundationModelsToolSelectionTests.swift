import Foundation
import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import VouchaTestSupport
import XCTest

#if canImport(FoundationModels)
    @MainActor
    @available(macOS 26.4, iOS 26.4, *)
    final class NativeChatFoundationModelsToolSelectionTests: XCTestCase {
        func testPublishedCatalogSelectsSmallRelevantSubsetForCards() throws {
            let root = try FilamentsContractRoot.url(requiredPaths: ["api-fixtures/v1/mcp.json"])
            let url = try FilamentsContractRoot.requiredURL("api-fixtures/v1/mcp.json", root: root)
            let manifest = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(contentsOf: url)) as? [String: Any])
            let servers = try XCTUnwrap(manifest["servers"] as? [[String: Any]])
            let server = try XCTUnwrap(servers.first { $0["surface"] as? String == "mcp" })
            let entries = try XCTUnwrap(server["tools"] as? [[String: Any]])
            let catalog = try JSONDecoder().decode(DecodedJSONValue.self, from: JSONSerialization.data(
                withJSONObject: ["tools": entries.map { try XCTUnwrap($0["tool"] as? [String: Any]) }]
            ))
            let bridge = try NativeChatMCPToolBridge(catalog: catalog)
            XCTAssertEqual(bridge.tools.count, 69)

            let selected = NativeChatFoundationModelsToolSelection.select(bridge, matching: "Show my cards")
            XCTAssertTrue(selected.contains { $0.descriptor.name == "get_my_cards" })
            XCTAssertLessThanOrEqual(selected.count, NativeChatFoundationModelsToolSelection.maximumTools)
            XCTAssertLessThanOrEqual(selected.reduce(0) { total, item in
                total + item.descriptor.name.utf8.count + item.descriptor.description.utf8.count
                    + item.descriptor.parameters.count
            }, NativeChatFoundationModelsToolSelection.maximumCatalogBytes)
            XCTAssertLessThanOrEqual(NativeChatAppleFoundationModelsProvider.assistantPrompt(
                message: String(repeating: "cards ", count: 500), history: [],
                maximumCharacters: NativeChatFoundationModelsToolSelection.maximumPromptCharacters
            ).count, NativeChatFoundationModelsToolSelection.maximumPromptCharacters)
        }

        func testUnsupportedRelevantMapSkipsToValidToolAndUnrelatedMessageUsesNoTools() throws {
            let catalog: DecodedJSONValue = .object(["tools": .array([
                .object([
                    "name": .string("read_community_map"),
                    "description": .string("Read community map"),
                    "inputSchema": .object([
                        "type": .string("object"),
                        "additionalProperties": .object(["type": .string("string")])
                    ])
                ]),
                .object([
                    "name": .string("read_community"),
                    "description": .string("Read community"),
                    "inputSchema": .object([
                        "type": .string("object"),
                        "properties": .object(["id": .object(["type": .string("string")])]),
                        "additionalProperties": .bool(false)
                    ])
                ])
            ])])
            let bridge = try NativeChatMCPToolBridge(catalog: catalog)
            let selected = NativeChatFoundationModelsToolSelection.select(bridge, matching: "read community")
            XCTAssertEqual(selected.map(\.descriptor.name), ["read_community"])
            XCTAssertTrue(NativeChatFoundationModelsToolSelection.select(bridge, matching: "translate French").isEmpty)
        }
    }
#endif
