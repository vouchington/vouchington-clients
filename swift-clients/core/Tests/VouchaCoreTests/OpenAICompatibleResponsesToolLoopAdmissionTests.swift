import Foundation
@testable import VouchaCore
import XCTest

final class OpenAICompatibleResponsesToolLoopAdmissionTests: XCTestCase {
    private var endpoint: LocalLLMEndpointProfile {
        LocalLLMEndpointProfile(
            id: UUID(), endpoint: "http://localhost:2999/v1", modelNames: ["gpt-oss"],
            selectedModelName: "gpt-oss"
        )
    }

    private func request(history: [LocalLLMChatMessage] = []) -> LocalLLMToolRequest {
        LocalLLMToolRequest(
            message: "Update it", history: history, endpoint: endpoint, apiKey: nil,
            tools: [.init(name: "write", description: "Write", parameters: Data(#"{"type":"object"}"#.utf8))]
        )
    }

    func testRejectedBeforeServiceAdmissionKeepsFirstTurnRetryable() async throws {
        ToolLoopURLProtocol.reset(responses: [
            #"{"output":[{"type":"function_call","call_id":"write-1","name":"write","arguments":"{}"}]}"#
        ])
        let client = OpenAICompatibleResponsesClient(protocolClasses: [ToolLoopURLProtocol.self])
        do {
            _ = try await client.generateAssistantResponseWithTools(
                request: request(), isCurrent: { true }
            ) { _, _ in
                throw LocalLLMToolLoopFailure.toolNotAdmitted
            }
            XCTFail("Rejected pre-admission call must cancel without a partial side effect")
        } catch is CancellationError {}
        XCTAssertEqual(ToolLoopURLProtocol.bodies().count, 1)
    }

    func testOversizedMinimalContinuationRejectsBeforeMutation() async throws {
        let ignored = String(repeating: "x", count: 995_000)
        let response: [String: Any] = ["output": [
            ["type": "reasoning", "summary": [["type": "summary_text", "text": ignored]]],
            ["type": "function_call", "call_id": "write-1", "name": "write", "arguments": "{}"]
        ]]
        let data = try JSONSerialization.data(withJSONObject: response)
        XCTAssertLessThan(data.count, 1_000_000)
        let responseText = try XCTUnwrap(String(data: data, encoding: .utf8))
        ToolLoopURLProtocol.reset(responses: [responseText, #"{"output":[],"output_text":"unexpected"}"#])
        let client = OpenAICompatibleResponsesClient(protocolClasses: [ToolLoopURLProtocol.self])
        let history = [LocalLLMChatMessage(role: "user", content: String(repeating: "h", count: 12_000))]
        let invocations = ToolInvocationCounter()
        do {
            _ = try await client.generateAssistantResponseWithTools(
                request: request(history: history), isCurrent: { true }
            ) { _, _ in
                await invocations.record()
                return "{}"
            }
            XCTFail("Continuation that cannot fit an empty result must be rejected")
        } catch {
            XCTAssertEqual(error as? LocalLLMToolLoopFailure, .resultTooLarge)
        }
        let count = await invocations.count
        XCTAssertEqual(count, 0)
        XCTAssertEqual(ToolLoopURLProtocol.bodies().count, 1)
    }

    func testContinuationReservesAllowedResultBeforeMutation() async throws {
        let ignored = String(repeating: "x", count: 800_000)
        let response: [String: Any] = ["output": [
            ["type": "reasoning", "summary": [["type": "summary_text", "text": ignored]]],
            ["type": "function_call", "call_id": "write-1", "name": "write", "arguments": "{}"]
        ]]
        let data = try JSONSerialization.data(withJSONObject: response)
        XCTAssertLessThan(data.count, 1_000_000)
        let responseText = try XCTUnwrap(String(data: data, encoding: .utf8))
        ToolLoopURLProtocol.reset(responses: [responseText, #"{"output":[],"output_text":"unexpected"}"#])
        let client = OpenAICompatibleResponsesClient(protocolClasses: [ToolLoopURLProtocol.self])
        let invocations = ToolInvocationCounter()
        do {
            _ = try await client.generateAssistantResponseWithTools(
                request: request(), isCurrent: { true }
            ) { _, _ in
                await invocations.record()
                return String(repeating: "x", count: 256_000)
            }
            XCTFail("A tool result could overflow the next request after mutation")
        } catch {
            XCTAssertEqual(error as? LocalLLMToolLoopFailure, .resultTooLarge)
        }
        let count = await invocations.count
        XCTAssertEqual(count, 0)
        XCTAssertEqual(ToolLoopURLProtocol.bodies().count, 1)
    }
}
