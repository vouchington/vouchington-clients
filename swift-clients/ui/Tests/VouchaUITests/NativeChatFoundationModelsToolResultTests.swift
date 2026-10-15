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
    final class NativeChatFoundationModelsToolResultTests: XCTestCase {
        func testLargeRpcResultIsBoundedAfterOneAdmittedCall() async throws {
            let service = BridgeMCPService()
            let result = #"{"content":[{"type":"text","text":""# + String(repeating: "x", count: 100_000)
                + #""}],"isError":false}"#
            service.toolResult = try JSONDecoder().decode(MemberMCPClient.ToolResult.self, from: Data(result.utf8))
            service.catalog = .object(["tools": .array([.object([
                "name": .string("read_community"),
                "inputSchema": .object(["type": .string("object"), "additionalProperties": .bool(false)])
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

            let output = try await tool.call(arguments: GeneratedContent(json: "{}"))
            XCTAssertEqual(service.calls, 1)
            XCTAssertTrue(state.attempted)
            XCTAssertLessThanOrEqual(output.utf8.count, NativeChatFoundationModelsTool.maximumModelResultBytes)
            let envelope = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(output.utf8)) as? [String: Any])
            XCTAssertEqual(envelope["isError"] as? Bool, false)
            XCTAssertEqual(envelope["truncated"] as? Bool, true)
        }

        func testFourAdmittedCallsShareOneModelResultBudget() async throws {
            let service = BridgeMCPService()
            let result = #"{"content":[{"type":"text","text":""# + String(repeating: "x", count: 100_000)
                + #""}],"isError":false}"#
            service.toolResult = try JSONDecoder().decode(MemberMCPClient.ToolResult.self, from: Data(result.utf8))
            service.catalog = .object(["tools": .array([.object([
                "name": .string("read_community"),
                "inputSchema": .object(["type": .string("object"), "additionalProperties": .bool(false)])
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

            var total = 0
            for _ in 0 ..< 4 {
                let output = try await tool.call(arguments: GeneratedContent(json: "{}"))
                total += output.utf8.count
                XCTAssertLessThanOrEqual(output.utf8.count, NativeChatFoundationModelsTool.maximumModelResultBytes)
            }
            XCTAssertEqual(service.calls, 4)
            XCTAssertEqual(state.deliveredResultBytes, total)
            XCTAssertLessThanOrEqual(total, NativeChatFoundationModelsToolState.maximumSessionResultBytes)
            do {
                _ = try await tool.call(arguments: GeneratedContent(json: "{}"))
                XCTFail("A fifth MCP call must be rejected before admission")
            } catch is LocalLLMToolLoopFailure {
                XCTAssertEqual(service.calls, 4)
            }
        }
    }
#endif
