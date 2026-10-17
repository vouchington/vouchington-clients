import Foundation
import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import VouchaTestSupport
import XCTest

#if canImport(FoundationModels)
    import FoundationModels

    @MainActor
    @available(macOS 26.4, iOS 26.4, *)
    final class NativeChatFoundationModelsToolResultTests: XCTestCase {
        func testRealProfileFullEnvelopeIsRetainedBeforeCountingAndReturnedUnchanged() async throws {
            let service = BridgeMCPService()
            let fixture = try profileFixture()
            service.catalog = fixture.catalog
            service.toolResult = fixture.result
            let admission = ReceiptAdmission()
            let (tool, state) = try await makeTool(service: service, admission: admission)
            admission.onRetain = { envelope in
                XCTAssertEqual(state.receipts.first?.envelope, envelope)
            }

            let output = try await tool.call(arguments: GeneratedContent(json: fixture.arguments))
            XCTAssertEqual(service.calls, 1)
            XCTAssertEqual(state.receipts.first?.envelope, output)
            XCTAssertEqual(state.receipts.first?.kind, .response)
            let value = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(output.utf8)) as? NSDictionary)
            XCTAssertEqual(
                value["content"] as? [[String: String]],
                [["type": "text", "text": "synthetic <é/ sentinel"]]
            )
            XCTAssertTrue(try XCTUnwrap(value["structuredContent"] as? NSDictionary).isEqual(to: fixture.structured))
            XCTAssertEqual(value["isError"] as? Bool, false)
            XCTAssertNil(value["truncated"])
        }

        func testLargeValidatedResultStopsAfterOneCallWithoutPreviewOrRedispatch() async throws {
            let service = BridgeMCPService()
            let text = String(repeating: "<é/receipt>", count: 10_000)
            service.toolResult = try result(text: text)
            service.catalog = plainCatalog()
            let admission = ReceiptAdmission()
            admission.continuationFits = false
            let (tool, state) = try await makeTool(service: service, admission: admission)
            do {
                _ = try await tool.call(arguments: GeneratedContent(json: "{}"))
                XCTFail("A full result that cannot fit must stop continuation")
            } catch let stop as NativeChatFoundationModelsStop {
                XCTAssertEqual(stop.reason, .contextStopped)
            }
            XCTAssertEqual(service.calls, 1)
            let receipt = try XCTUnwrap(state.receipts.first)
            let body = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(receipt.envelope.utf8)) as? NSDictionary)
            XCTAssertEqual((body["content"] as? [[String: String]])?.first?["text"], text)
            XCTAssertNil(body["truncated"])
            XCTAssertFalse(state.hasAmbiguousAttempt)
            do { _ = try await tool.call(arguments: GeneratedContent(json: "{}"))
                XCTFail("Stopped turn must not redispatch")
            } catch { XCTAssertEqual(service.calls, 1) }
        }

        func testCancellationWhileCountingKeepsReceiptButDoesNotReturnResultToModel() async throws {
            let service = BridgeMCPService()
            service.catalog = plainCatalog()
            let admission = ReceiptAdmission()
            admission.holdRetention = true
            let counting = expectation(description: "counting after validated receipt")
            admission.onRetain = { _ in counting.fulfill() }
            let (tool, state) = try await makeTool(service: service, admission: admission)
            let call = Task { try await tool.call(arguments: GeneratedContent(json: "{}")) }
            await fulfillment(of: [counting], timeout: 5)
            XCTAssertEqual(state.receipts.count, 1)
            call.cancel()
            admission.releaseRetention()
            do {
                _ = try await call.value
                XCTFail("Cancelled result must not reach the model")
            } catch is CancellationError {} catch { XCTFail("Expected cancellation: \(error)") }
            XCTAssertEqual(service.calls, 1)
            XCTAssertEqual(state.receipts.count, 1)
            XCTAssertFalse(state.hasAmbiguousAttempt)
        }

        func testAccountChangeWhileCountingDoesNotReturnPriorAccountResult() async throws {
            let service = BridgeMCPService()
            service.catalog = plainCatalog()
            let admission = ReceiptAdmission()
            admission.holdRetention = true
            let counting = expectation(description: "counting prior-account receipt")
            admission.onRetain = { _ in counting.fulfill() }
            let (tool, state) = try await makeTool(service: service, admission: admission)
            let call = Task { try await tool.call(arguments: GeneratedContent(json: "{}")) }
            await fulfillment(of: [counting], timeout: 5)
            await tool.session.useAccount("member-b")
            admission.releaseRetention()
            do {
                _ = try await call.value
                XCTFail("Prior account result must not reach the model")
            } catch is CancellationError {} catch { XCTFail("Expected stale-account cancellation: \(error)") }
            XCTAssertEqual(service.calls, 1)
            XCTAssertEqual(state.receipts.count, 1)
            XCTAssertEqual(state.receipts.first?.owner.accountId, "member-a")
            XCTAssertFalse(state.hasAmbiguousAttempt)
        }

        func testToolErrorIsRetainedAsAnErrorResponse() async throws {
            let service = BridgeMCPService()
            service.catalog = plainCatalog()
            service.toolResult = try result(text: "synthetic refusal", isError: true)
            let (tool, state) = try await makeTool(service: service, admission: ReceiptAdmission())
            let output = try await tool.call(arguments: GeneratedContent(json: "{}"))
            XCTAssertEqual(service.calls, 1)
            XCTAssertEqual(state.receipts.first?.kind, .toolError)
            XCTAssertEqual(state.receipts.first?.envelope, output)
            XCTAssertFalse(state.hasAmbiguousAttempt)
        }

        func testExistingFourCallAndArgumentBudgetsStillRejectBeforeRpc() async throws {
            let service = BridgeMCPService()
            service.catalog = plainCatalog()
            let (tool, state) = try await makeTool(service: service, admission: ReceiptAdmission())
            for _ in 0 ..< 4 {
                _ = try await tool.call(arguments: GeneratedContent(json: "{}"))
            }
            do { _ = try await tool.call(arguments: GeneratedContent(json: "{}"))
                XCTFail("Fifth call must not dispatch")
            } catch { XCTAssertEqual(service.calls, 4) }
            XCTAssertEqual(state.receipts.count, 4)

            let largeService = BridgeMCPService()
            largeService.catalog = .object(["tools": .array([.object([
                "name": .string("read_community"), "inputSchema": .object([
                    "type": .string("object"), "properties": .object(["payload": .object(["type": .string("string")])]),
                    "required": .array([.string("payload")]), "additionalProperties": .bool(false)
                ])
            ])])])
            let (largeTool, _) = try await makeTool(service: largeService, admission: ReceiptAdmission())
            let arguments = try GeneratedContent(json: #"{"payload":"\#(String(repeating: "x", count: 550))"}"#)
            _ = try await largeTool.call(arguments: arguments)
            _ = try await largeTool.call(arguments: arguments)
            do { _ = try await largeTool.call(arguments: arguments)
                XCTFail("Session argument budget must remain enforced")
            } catch { XCTAssertEqual(largeService.calls, 2) }
        }

        func testContextStopBlocksSiblingWaitingBeforeRpcAdmission() async throws {
            let service = try HeldReceiptService(result: result(text: "complete"))
            let firstStarted = expectation(description: "first RPC admitted")
            service.onStarted = { if service.calls == 1 { firstStarted.fulfill() } }
            let admission = ReceiptAdmission()
            admission.continuationFits = false
            admission.holdPreparationCall = 2
            let siblingHeld = expectation(description: "sibling waiting before admission")
            admission.onPrepared = { if admission.prepareCalls == 2 { siblingHeld.fulfill() } }
            let (tool, state) = try await makeTool(service: service, admission: admission)
            let arguments = try GeneratedContent(json: "{}")
            let first = Task { try await tool.call(arguments: arguments) }
            await fulfillment(of: [firstStarted], timeout: 5)
            let sibling = Task { try await tool.call(arguments: arguments) }
            await fulfillment(of: [siblingHeld], timeout: 5)
            service.release(0)
            do { _ = try await first.value
                XCTFail("First result must stop continuation")
            } catch {}
            admission.releasePreparation()
            do { _ = try await sibling.value
                XCTFail("Queued sibling must not dispatch")
            } catch {}
            XCTAssertEqual(service.calls, 1)
            XCTAssertEqual(state.receipts.count, 1)
            XCTAssertFalse(state.hasAmbiguousAttempt)
        }

        func testAlreadyStartedUnknownSiblingStaysAmbiguousBesideKnownReceipt() async throws {
            let service = try HeldReceiptService(result: result(text: "complete"))
            let firstStarted = expectation(description: "first RPC admitted")
            let bothStarted = expectation(description: "both RPCs admitted")
            service.onStarted = {
                if service.calls == 1 { firstStarted.fulfill() }
                if service.calls == 2 { bothStarted.fulfill() }
            }
            let admission = ReceiptAdmission()
            admission.continuationFits = false
            let (tool, state) = try await makeTool(service: service, admission: admission)
            let arguments = try GeneratedContent(json: "{}")
            let first = Task { try await tool.call(arguments: arguments) }
            await fulfillment(of: [firstStarted], timeout: 5)
            let sibling = Task { try await tool.call(arguments: arguments) }
            await fulfillment(of: [bothStarted], timeout: 5)
            service.release(0)
            do { _ = try await first.value
                XCTFail("Known result stops continuation")
            } catch {}
            XCTAssertEqual(state.receipts.count, 1)
            XCTAssertTrue(state.hasAmbiguousAttempt)
            service.release(1, failure: true)
            do { _ = try await sibling.value
                XCTFail("Unknown sibling must remain an error")
            } catch {}
            XCTAssertEqual(service.calls, 2)
            XCTAssertEqual(state.receipts.count, 1)
            XCTAssertTrue(state.hasAmbiguousAttempt)
        }

        private func makeTool(
            service: any NativeChatMCPServicing, admission: ReceiptAdmission
        ) async throws -> (NativeChatFoundationModelsTool, NativeChatFoundationModelsToolState) {
            let session = NativeChatMCPAgentSession(service: service)
            await session.useAccount("member-a")
            try await session.connect()
            let snapshot = try XCTUnwrap(session.connectedSnapshot())
            let bridge = try NativeChatMCPToolBridge(catalog: snapshot.catalog)
            let descriptor = try XCTUnwrap(bridge.tools.first)
            let state = NativeChatFoundationModelsToolState(turn: .fixture(snapshot))
            return try (NativeChatFoundationModelsTool(
                name: descriptor.name, description: descriptor.description,
                parameters: NativeChatFoundationModelsSchema.project(descriptor.parameters),
                bridge: bridge, session: session, snapshot: snapshot, state: state, admission: admission
            ), state)
        }

        private func result(text: String, isError: Bool = false) throws -> MemberMCPClient.ToolResult {
            try JSONDecoder().decode(MemberMCPClient.ToolResult.self, from: JSONSerialization.data(withJSONObject: [
                "content": [["type": "text", "text": text]], "isError": isError
            ]))
        }

        private func plainCatalog() -> DecodedJSONValue {
            .object(["tools": .array([.object([
                "name": .string("read_community"),
                "inputSchema": .object(["type": .string("object"), "additionalProperties": .bool(false)])
            ])])])
        }

        private func profileFixture() throws -> (
            catalog: DecodedJSONValue, result: MemberMCPClient.ToolResult, arguments: String, structured: [String: Any]
        ) {
            let root = try FilamentsContractRoot.url(requiredPaths: [
                "api-fixtures/v1/mcp.json",
                "api-fixtures/v1/mcp-results.json"
            ])
            func object(_ path: String) throws -> [String: Any] {
                try XCTUnwrap(JSONSerialization
                    .jsonObject(with: Data(contentsOf: root.appendingPathComponent(path))) as? [String: Any])
            }
            let servers = try XCTUnwrap(object("api-fixtures/v1/mcp.json")["servers"] as? [[String: Any]])
            let server = try XCTUnwrap(servers.first { $0["name"] as? String == "voucha-user-mcp" })
            let entries = try XCTUnwrap(server["tools"] as? [[String: Any]])
            let tool = try XCTUnwrap(entries.compactMap { $0["tool"] as? [String: Any] }
                .first { $0["name"] as? String == "read_my_profile" })
            let rows = try XCTUnwrap(object("api-fixtures/v1/mcp-results.json")["cases"] as? [[String: Any]])
            let row = try XCTUnwrap(rows.first { $0["id"] as? String == "native.mcp.read-my-profile.bio" })
            let structured = try XCTUnwrap(row["structuredContent"] as? [String: Any])
            let result = try JSONDecoder().decode(
                MemberMCPClient.ToolResult.self,
                from: JSONSerialization.data(withJSONObject: [
                    "content": [["type": "text", "text": "synthetic <é/ sentinel"]], "isError": false,
                    "structuredContent": structured
                ])
            )
            let arguments = try XCTUnwrap(row["arguments"])
            return try (
                JSONDecoder().decode(
                    DecodedJSONValue.self,
                    from: JSONSerialization.data(withJSONObject: ["tools": [tool]])
                ),
                result, XCTUnwrap(String(data: JSONSerialization.data(withJSONObject: arguments), encoding: .utf8)),
                structured
            )
        }
    }

    @MainActor
    private final class HeldReceiptService: NativeChatMCPServicing {
        enum Failure: Error { case unknownResult }
        var onStarted: (() -> Void)?
        private(set) var calls = 0
        private var pending: [Int: CheckedContinuation<MemberMCPClient.ToolResult, Error>] = [:]
        let result: MemberMCPClient.ToolResult
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

        func disconnect(accountId _: String) async throws {}
        func callTool(
            accountId _: String,
            name _: String,
            arguments _: [String: DecodedJSONValue]
        ) async throws -> MemberMCPClient.ToolResult {
            let index = calls
            calls += 1
            return try await withCheckedThrowingContinuation { continuation in
                pending[index] = continuation
                onStarted?()
            }
        }

        func release(_ index: Int, failure: Bool = false) {
            guard let continuation = pending.removeValue(forKey: index) else { return }
            if failure { continuation.resume(throwing: Failure.unknownResult) }
            else { continuation.resume(returning: result) }
        }
    }
#endif
