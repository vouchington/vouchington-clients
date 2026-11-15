import Foundation

extension OpenAICompatibleResponsesClient {
    static func requireCompletedResponse(_ response: [String: Any], output: [[String: Any]]) throws {
        if let status = response["status"], status as? String != "completed" {
            throw LocalLLMToolLoopFailure.invalidResponse
        }
        for item in output where item["type"] as? String == "message" {
            if let status = item["status"], status as? String != "completed" {
                throw LocalLLMToolLoopFailure.invalidResponse
            }
        }
    }

    static func consumeCallBudget(_ count: Int, remaining: inout Int) throws {
        guard count <= remaining else { throw LocalLLMToolLoopFailure.tooManyCalls }
        remaining -= count
    }

    static func requireSingleCall(_ calls: [LocalLLMFunctionCall]) throws {
        guard calls.count == 1 else { throw LocalLLMToolLoopFailure.invalidResponse }
    }

    static func recordUniqueCallIDs(
        _ calls: [LocalLLMFunctionCall], seen: inout Set<String>
    ) throws {
        for call in calls {
            guard seen.insert(call.callID).inserted else {
                throw LocalLLMToolLoopFailure.invalidResponse
            }
        }
    }
}
