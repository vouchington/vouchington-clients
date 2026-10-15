import Foundation

extension NativeChatAppleFoundationModelsProvider {
    static func chatTranscriptLine(_ message: NativeChatTimelineMessage) -> String {
        "\(message.role.rawValue): \(message.content.trimmingCharacters(in: .whitespacesAndNewlines))"
    }

    static func utf8Prefix(_ text: String, limit: Int) -> String {
        guard limit > 0 else { return "" }
        var bytes = 0
        var result = ""
        for character in text {
            let size = String(character).utf8.count
            guard bytes + size <= limit else { break }
            result.append(character)
            bytes += size
        }
        return result
    }
}
