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
            let server = try XCTUnwrap(servers.first { $0["name"] as? String == "voucha-user-mcp" })
            let entries = try XCTUnwrap(server["tools"] as? [[String: Any]])
            let catalog = try JSONDecoder().decode(DecodedJSONValue.self, from: JSONSerialization.data(
                withJSONObject: ["tools": entries.map { try XCTUnwrap($0["tool"] as? [String: Any]) }]
            ))
            let bridge = try NativeChatMCPToolBridge(catalog: catalog)
            XCTAssertEqual(bridge.tools.count, entries.count)

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

        func testContextualFollowUpSelectsToolFromBoundedPromptHistory() throws {
            let bridge = try NativeChatMCPToolBridge(catalog: .object(["tools": .array([.object([
                "name": .string("get_my_cards"),
                "description": .string("Read my cards"),
                "inputSchema": .object(["type": .string("object"), "additionalProperties": .bool(false)])
            ])])]))
            let history: [NativeChatTimelineMessage] = [
                .init(id: "request", role: .user, content: "Show my cards"),
                .init(id: "answer", role: .assistant, content: "I can show them.")
            ]
            let context = NativeChatAppleFoundationModelsProvider.boundedAssistantContext(
                message: "Yes, do that", history: history,
                maximumCharacters: NativeChatFoundationModelsToolSelection.maximumPromptCharacters
            )
            let selected = NativeChatAppleFoundationModelsProvider.projectedTools(
                from: bridge, message: "Yes, do that", history: history
            )
            XCTAssertEqual(selected.map(\.descriptor.name), ["get_my_cards"])
            let prompt = NativeChatAppleFoundationModelsProvider.assistantPrompt(
                message: "Yes, do that", history: history,
                maximumCharacters: NativeChatFoundationModelsToolSelection.maximumPromptCharacters
            )
            XCTAssertTrue(prompt.contains(context.transcript))
            XCTAssertLessThanOrEqual(prompt.count, NativeChatFoundationModelsToolSelection.maximumPromptCharacters)

            let staleHistory = [history[0]] + (0 ..< 12).map { index in
                NativeChatTimelineMessage(id: "later-\(index)", role: .user, content: "Other topic")
            }
            XCTAssertTrue(NativeChatAppleFoundationModelsProvider.projectedTools(
                from: bridge, message: "Yes, do that", history: staleHistory
            ).isEmpty)
        }

        func testTranscriptRoleLabelsDoNotSelectUnrequestedTools() throws {
            let bridge = try NativeChatMCPToolBridge(catalog: .object(["tools": .array([.object([
                "name": .string("list_assistant_notes"),
                "description": .string("Read assistant notes"),
                "inputSchema": .object(["type": .string("object"), "additionalProperties": .bool(false)])
            ])])]))
            let history = [
                NativeChatTimelineMessage(id: "first", role: .user, content: "What time is it?"),
                NativeChatTimelineMessage(id: "second", role: .assistant, content: "It is noon.")
            ]
            let context = NativeChatAppleFoundationModelsProvider.boundedAssistantContext(
                message: "Thanks", history: history,
                maximumCharacters: NativeChatFoundationModelsToolSelection.maximumPromptCharacters
            )
            XCTAssertTrue(context.transcript.contains("user:"))
            XCTAssertFalse(context.selectionHistory.contains("user:"))
            XCTAssertFalse(context.selectionHistory.contains("assistant:"))
            XCTAssertTrue(NativeChatAppleFoundationModelsProvider.projectedTools(
                from: bridge, message: "Thanks", history: history
            ).isEmpty)
        }

        func testExactShortToolNameSurvivesTokenFiltering() throws {
            let bridge = try NativeChatMCPToolBridge(catalog: .object(["tools": .array([
                .object([
                    "name": .string("dm"),
                    "description": .string("Send a direct message"),
                    "inputSchema": .object(["type": .string("object"), "additionalProperties": .bool(false)])
                ]),
                .object([
                    "name": .string("get_my_cards"),
                    "description": .string("Read saved cards"),
                    "inputSchema": .object(["type": .string("object"), "additionalProperties": .bool(false)])
                ])
            ])]))
            XCTAssertEqual(
                NativeChatFoundationModelsToolSelection.select(bridge, matching: "DM Li").map(\.descriptor.name),
                ["dm"]
            )
            XCTAssertTrue(NativeChatFoundationModelsToolSelection.select(bridge, matching: "my").isEmpty)
        }

        func testTruncatedAntecedentNeverSelectsToolForContextualApproval() throws {
            let bridge = try NativeChatMCPToolBridge(catalog: .object(["tools": .array([.object([
                "name": .string("delete_my_cards"),
                "description": .string("Delete my cards"),
                "inputSchema": .object(["type": .string("object"), "additionalProperties": .bool(false)])
            ])])]))
            let request = "Delete my cards " + String(repeating: "context ", count: 180)
                + "except the travel card"
            let history = [NativeChatTimelineMessage(id: "request", role: .user, content: request)]
            let context = NativeChatAppleFoundationModelsProvider.boundedAssistantContext(
                message: "Yes, do that", history: history,
                maximumCharacters: NativeChatFoundationModelsToolSelection.maximumPromptCharacters
            )
            XCTAssertFalse(context.hasCompleteHistory)
            XCTAssertFalse(context.transcript.contains("except the travel card"))
            XCTAssertTrue(NativeChatAppleFoundationModelsProvider.projectedTools(
                from: bridge, message: "Yes, do that", history: history
            ).isEmpty)
            XCTAssertEqual(NativeChatAppleFoundationModelsProvider.projectedTools(
                from: bridge, message: "Yes, do that",
                history: [.init(id: "short", role: .user, content: "Delete my cards except the travel card")]
            ).map(\.descriptor.name), ["delete_my_cards"])
        }

        func testTruncatedLatestActionRequestNeverSelectsMCPTool() throws {
            let bridge = try NativeChatMCPToolBridge(catalog: .object(["tools": .array([.object([
                "name": .string("delete_my_cards"),
                "description": .string("Delete my cards"),
                "inputSchema": .object(["type": .string("object"), "additionalProperties": .bool(false)])
            ])])]))
            let complete = "Delete my cards " + String(repeating: "context ", count: 200) + "except the saved travel card"
            let context = NativeChatAppleFoundationModelsProvider.boundedAssistantContext(
                message: complete, history: [],
                maximumCharacters: NativeChatFoundationModelsToolSelection.maximumPromptCharacters
            )
            XCTAssertFalse(context.latestMessage.contains("except the saved travel card"))
            XCTAssertTrue(NativeChatAppleFoundationModelsProvider.projectedTools(
                from: bridge, message: complete, history: []
            ).isEmpty)
            XCTAssertEqual(NativeChatAppleFoundationModelsProvider.projectedTools(
                from: bridge, message: "Delete my cards", history: []
            ).map(\.descriptor.name), ["delete_my_cards"])
        }
    }
#endif
