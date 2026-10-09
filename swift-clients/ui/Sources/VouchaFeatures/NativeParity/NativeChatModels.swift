import VouchaLocalization
import VouchaModels

extension ChatMessage {
    var presentationError: UiVerbatimText? {
        if let error = content.error, !error.isEmpty { return .verbatim(error) }
        return completion?.status == "incomplete" ? .message(.nativeSwiftChatResponseInterrupted) : nil
    }
}

struct NativePaginationState {
    let hasNextPage: Bool
    let endCursor: String?
}

struct NativeChatTimelineMessage: Identifiable {
    enum Role: String { case user, assistant }

    var id: String
    var role: Role
    var content: String
    var isStreaming = false
    var error: UiVerbatimText?
}
