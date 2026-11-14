import Foundation
import VouchaLocalization
import VouchaModels

struct AndroidChatMessage: Identifiable, Equatable {
    let id: String
    let role: String
    let content: String
    let createdAt: Date
    let error: UiVerbatimText?

    init(
        id: String = UUID().uuidString,
        role: String,
        content: String,
        createdAt: Date = Date(),
        error: UiVerbatimText? = nil
    ) {
        self.id = id
        self.role = role
        self.content = content
        self.createdAt = createdAt
        self.error = error
    }
}

extension AndroidChatMessage {
    init(transcriptMessage message: ChatMessage) {
        self.init(
            id: message.id,
            role: message.content.role == "assistant" ? "assistant" : "user",
            content: message.content.displayText,
            createdAt: message.createdAt,
            error: Self.transcriptError(message)
        )
    }

    private static func transcriptError(_ message: ChatMessage) -> UiVerbatimText? {
        if let error = message.content.error, !error.isEmpty { return .verbatim(error) }
        return message.completion?.status == "incomplete" ? .message(.nativeSwiftChatResponseInterrupted) : nil
    }
}
