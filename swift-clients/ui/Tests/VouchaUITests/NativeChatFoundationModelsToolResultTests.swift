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
        func testFourCallArgumentBudgetRejectsBeforeThirdRpcAdmission() async throws {
            let service = BridgeMCPService()
            service.catalog = .object(["tools": .array([.object([
                "name": .string("read_community"),
                "inputSchema": .object([
                    "type": .string("object"),
                    "properties": .object(["payload": .object(["type": .string("string")])]),
                    "required": .array([.string("payload")]),
                    "additionalProperties": .bool(false)
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
            let arguments = try GeneratedContent(json: #"{"payload":"\#(String(repeating: "x", count: 550))"}"#)
            _ = try await tool.call(arguments: arguments)
            _ = try await tool.call(arguments: arguments)
            XCTAssertEqual(service.calls, 2)
            do {
                _ = try await tool.call(arguments: arguments)
                XCTFail("A third large argument must fail before RPC admission")
            } catch is LocalLLMToolLoopFailure {
                XCTAssertEqual(service.calls, 2)
            }
        }

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

        func testFourOverlappingAdmittedCallsShareOneModelResultBudget() async throws {
            let raw = #"{"content":[{"type":"text","text":""# + String(repeating: "x", count: 10_000)
                + #""}],"isError":false}"#
            let result = try JSONDecoder().decode(MemberMCPClient.ToolResult.self, from: Data(raw.utf8))
            let service = ParallelMCPResultService(result: result)
            let allHeld = expectation(description: "all four admitted MCP calls are held")
            service.allCallsHeld = allHeld
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
            let arguments = try GeneratedContent(json: "{}")
            let calls = (0 ..< 4).map { _ in Task { try await tool.call(arguments: arguments) } }

            await fulfillment(of: [allHeld], timeout: 5)
            XCTAssertEqual(service.pendingCount, 4)
            XCTAssertTrue(state.attempted)
            XCTAssertEqual(state.deliveredResultBytes, 0)
            service.releaseAll()

            var total = 0
            for call in calls {
                let output = try await call.value
                total += output.utf8.count
                XCTAssertLessThanOrEqual(output.utf8.count, NativeChatFoundationModelsTool.maximumModelResultBytes)
            }
            XCTAssertEqual(service.calls, 4)
            XCTAssertEqual(state.deliveredResultBytes, total)
            XCTAssertLessThanOrEqual(total, NativeChatFoundationModelsToolState.maximumSessionResultBytes)
        }

        func testMinimumResultFallbackPreservesSuccessfulRpcStatus() throws {
            for isError in [false, true] {
                let raw = #"{"content":[{"type":"text","text":""#
                    + String(repeating: "x", count: 10_000)
                    + #""}],"isError":\#(isError)}"#
                let bounded = NativeChatFoundationModelsTool.contextBoundResult(raw, maximumBytes: 80)
                XCTAssertLessThanOrEqual(bounded.utf8.count, 80)
                let envelope = try XCTUnwrap(JSONSerialization.jsonObject(
                    with: Data(bounded.utf8)
                ) as? [String: Any])
                XCTAssertEqual(envelope["isError"] as? Bool, isError)
                XCTAssertEqual(envelope["truncated"] as? Bool, true)
            }
        }
    }

    @MainActor
    private final class ParallelMCPResultService: NativeChatMCPServicing {
        var allCallsHeld: XCTestExpectation?
        private(set) var calls = 0
        private(set) var pendingCount = 0
        private var pending: [CheckedContinuation<MemberMCPClient.ToolResult, Never>] = []
        private let result: MemberMCPClient.ToolResult

        init(result: MemberMCPClient.ToolResult) {
            self.result = result
        }

        func connect(accountId _: String) async throws -> DecodedJSONValue {
            .object(["tools": .array([.object([
                "name": .string("read_community"),
                "inputSchema": .object(["type": .string("object"), "additionalProperties": .bool(false)])
            ])])])
        }

        func refreshTools(accountId: String) async throws -> DecodedJSONValue {
            try await connect(accountId: accountId)
        }

        func disconnect(accountId _: String) async throws {
            XCTAssertTrue(pending.isEmpty)
        }

        func callTool(
            accountId _: String, name _: String, arguments _: [String: DecodedJSONValue]
        ) async throws -> MemberMCPClient.ToolResult {
            calls += 1
            return await withCheckedContinuation { continuation in
                pending.append(continuation)
                pendingCount = pending.count
                if pendingCount == 4 { allCallsHeld?.fulfill() }
            }
        }

        func releaseAll() {
            for continuation in pending {
                continuation.resume(returning: result)
            }
            pending.removeAll()
        }
    }
#endif
