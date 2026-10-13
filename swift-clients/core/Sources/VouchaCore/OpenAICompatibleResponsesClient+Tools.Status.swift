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
}
