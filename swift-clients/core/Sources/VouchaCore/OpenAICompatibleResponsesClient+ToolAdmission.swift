import Foundation

extension OpenAICompatibleResponsesClient {
    struct ToolPreflightContext {
        let model: String
        let definitions: [[String: Any]]
    }

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
        _ call: LocalLLMFunctionCall, input: [[String: Any]],
        context: ToolPreflightContext,
        validate: @Sendable (String, String) async throws -> Void
    ) async throws {
        try Task.checkCancellation()
        try await validate(call.name, call.arguments)
        // The bridge returns a JSON-encoded envelope without literal control characters.
        // Backslashes reserve the maximum outer-JSON escaping for every allowed result byte.
        let reservedInput = input + [[
            "type": "function_call_output", "call_id": call.callID,
            "output": String(repeating: "\\", count: 256_000)
        ]]
        guard try requestBody(
            model: context.model, input: reservedInput, definitions: context.definitions
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
