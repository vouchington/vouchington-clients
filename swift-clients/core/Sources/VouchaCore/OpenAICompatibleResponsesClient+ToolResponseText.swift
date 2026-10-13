import Foundation

extension OpenAICompatibleResponsesClient {
    static func responseText(_ response: [String: Any], output: [[String: Any]]) -> String? {
        if let text = response["output_text"] as? String {
            let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)
            if !trimmed.isEmpty { return trimmed }
        }
        let text = output.filter { $0["type"] as? String == "message" }
            .compactMap { $0["content"] as? [[String: Any]] }
            .flatMap { $0 }.compactMap { part in
                switch part["type"] as? String {
                case "output_text": part["text"] as? String
                case "refusal": part["refusal"] as? String
                default: nil
                }
            }.joined()
            .trimmingCharacters(in: .whitespacesAndNewlines)
        return text.isEmpty ? nil : text
    }
}
