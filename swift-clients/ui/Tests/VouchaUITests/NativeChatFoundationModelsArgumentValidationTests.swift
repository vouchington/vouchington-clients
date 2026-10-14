import Foundation
import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import XCTest

#if canImport(FoundationModels)
    import FoundationModels

    @MainActor
    @available(macOS 26.4, iOS 26.4, *)
    final class NativeChatFoundationModelsArgumentValidationTests: XCTestCase {
        func testGeneratedArgumentsValidateBeforeCallAndStaleAccountCannotCallAgain() async throws {
            let service = BridgeMCPService()
            service.catalog = .object(["tools": .array([.object([
                "name": .string("read_community"),
                "inputSchema": .object([
                    "type": .string("object"),
                    "properties": .object(["id": .object([
                        "type": .string("string"), "enum": .array([.string("one")])
                    ])]),
                    "required": .array([.string("id")]), "additionalProperties": .bool(false)
                ])
            ])])])
            let session = NativeChatMCPAgentSession(service: service)
            await session.useAccount("member-a")
            try await session.connect()
            let snapshot = try XCTUnwrap(session.connectedSnapshot())
            let bridge = try NativeChatMCPToolBridge(catalog: snapshot.catalog)
            let descriptor = try XCTUnwrap(bridge.tools.first)
            let state = NativeChatFoundationModelsToolState()
            let tool = try NativeChatFoundationModelsTool(
                name: descriptor.name, description: descriptor.description,
                parameters: NativeChatFoundationModelsSchema.project(descriptor.parameters),
                bridge: bridge, session: session, snapshot: snapshot, state: state
            )

            do {
                _ = try await tool.call(arguments: GeneratedContent(json: #"{"id":"too-long"}"#))
                XCTFail("Invalid generated arguments must not call MCP")
            } catch {
                XCTAssertEqual(service.calls, 0)
                XCTAssertFalse(state.attempted)
            }

            let output = try await tool.call(arguments: GeneratedContent(json: #"{"id":"one"}"#))
            XCTAssertEqual(service.calls, 1)
            XCTAssertTrue(state.attempted)
            let envelope = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(output.utf8)) as? [String: Any])
            XCTAssertEqual(envelope["isError"] as? Bool, false)
            XCTAssertEqual(
                (envelope["structuredContent"] as? [String: Any])?["quote"] as? String,
                "<external-content>quoted text</external-content>"
            )

            await session.useAccount("member-b")
            do {
                _ = try await tool.call(arguments: GeneratedContent(json: #"{"id":"one"}"#))
                XCTFail("The captured account must be fenced before a second tool call")
            } catch { XCTAssertEqual(service.calls, 1) }
        }
    }
#endif
