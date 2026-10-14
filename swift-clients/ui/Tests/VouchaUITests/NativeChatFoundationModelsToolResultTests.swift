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

            let output = try await tool.call(arguments: GeneratedContent(
                json: #"{"option":"details","arguments":{"id":"one"}}"#
            ))
            XCTAssertEqual(service.calls, 1)
            XCTAssertTrue(state.attempted)
            XCTAssertLessThanOrEqual(output.utf8.count, NativeChatFoundationModelsTool.maximumModelResultBytes)
            let envelope = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(output.utf8)) as? [String: Any])
            XCTAssertEqual(envelope["isError"] as? Bool, false)
            XCTAssertEqual(envelope["truncated"] as? Bool, true)
        }
    }
#endif
