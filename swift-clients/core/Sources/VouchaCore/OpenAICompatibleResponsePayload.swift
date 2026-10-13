import Foundation

struct ResponsePayload: Decodable {
    let outputText: String?

    enum CodingKeys: String, CodingKey {
        case outputText = "output_text"
        case output
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        if let outputText = try container.decodeIfPresent(String.self, forKey: .outputText) {
            let trimmedOutputText = outputText.trimmingCharacters(in: .whitespacesAndNewlines)
            self.outputText = trimmedOutputText.isEmpty ? nil : trimmedOutputText
            return
        }
        let output = try container.decodeIfPresent([ResponseOutput].self, forKey: .output) ?? []
        let text = output
            .flatMap { $0.content ?? [] }
            .compactMap(\.text)
            .joined()
            .trimmingCharacters(in: .whitespacesAndNewlines)
        outputText = text.isEmpty ? nil : text
    }
}

private struct ResponseOutput: Decodable {
    let content: [ResponseContent]?
}

private struct ResponseContent: Decodable {
    let text: String?
}
