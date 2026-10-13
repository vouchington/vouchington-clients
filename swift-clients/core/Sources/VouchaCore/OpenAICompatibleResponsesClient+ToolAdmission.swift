import Foundation

extension OpenAICompatibleResponsesClient {
    static func requestBody(
        model: String, input: [[String: Any]], definitions: [[String: Any]]
    ) throws -> Data {
        try JSONSerialization.data(withJSONObject: [
            "model": model, "instructions": toolResultInstructions,
            "input": input, "tools": definitions,
            "parallel_tool_calls": false
        ])
    }

    static func preflight(
        _ call: LocalLLMFunctionCall, model: String, input: [[String: Any]],
        definitions: [[String: Any]], validate: @Sendable (String, String) async throws -> Void
    ) async throws {
        try Task.checkCancellation()
        try await validate(call.name, call.arguments)
        // Response items other than the call also enter the next request. Reject a
        // continuation that cannot fit even an empty result before invoking a tool.
        let minimalInput = input + [[
            "type": "function_call_output", "call_id": call.callID, "output": ""
        ]]
        guard try requestBody(
            model: model, input: minimalInput, definitions: definitions
        ).count <= 1_000_000 else { throw LocalLLMToolLoopFailure.resultTooLarge }
    }

    static func invokeTool(
        _ call: LocalLLMFunctionCall, hadPreviousExecution: Bool,
        execute: @Sendable (String, String) async throws -> String
    ) async throws -> String {
        do {
            // The service may commit before an error or lost reply. Admission is the
            // only error that proves it was never called.
            return try await execute(call.name, call.arguments)
        } catch LocalLLMToolLoopFailure.toolNotAdmitted {
            if hadPreviousExecution { throw LocalLLMToolLoopFailure.partialExecution }
            throw CancellationError()
        } catch {
            throw LocalLLMToolLoopFailure.partialExecution
        }
    }
}
