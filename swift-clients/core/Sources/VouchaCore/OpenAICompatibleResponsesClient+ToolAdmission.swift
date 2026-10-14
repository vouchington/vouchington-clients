import Foundation

extension OpenAICompatibleResponsesClient {
    public static let maximumToolResultBytes = 1_100_000

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
        ], options: [.withoutEscapingSlashes])
    }

    static func preflight(
        _ call: LocalLLMFunctionCall, input: [[String: Any]],
        context: ToolPreflightContext,
        validate: @Sendable (String, String) async throws -> Void
    ) async throws {
        try Task.checkCancellation()
        try await validate(call.name, call.arguments)
        try Task.checkCancellation()
        // A producer result may contain Unicode that expands during outer JSON encoding.
        // Reserve six output bytes per allowed UTF-8 result byte without materializing it.
        let reservedInput = input + [[
            "type": "function_call_output", "call_id": call.callID,
            "output": ""
        ]]
        let emptyOutputBytes = try requestBody(
            model: context.model, input: reservedInput, definitions: context.definitions
        ).count
        guard emptyOutputBytes <= 20_000_000 - maximumToolResultBytes * 6 else {
            throw LocalLLMToolLoopFailure.resultTooLarge
        }
    }

    static func invokeTool(
        _ call: LocalLLMFunctionCall, hadPreviousExecution: Bool,
        execute: @Sendable (String, String) async throws -> String
    ) async throws -> String {
        try Task.checkCancellation()
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
