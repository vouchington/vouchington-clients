import VouchaLocalization
import VouchaModels

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
    var toolCalls: [ChatStreamToolCall] = []
    var toolResults: [NativeChatToolResult] = []
    var subagentSteps: [ChatStreamSubagentStep] = []
    var subagentTextChunks: [ChatStreamSubagentText] = []
    var error: UiVerbatimText?
}

struct NativeChatToolResult: Identifiable {
    let toolCallId: String
    let result: DecodedJSONValue

    var id: String {
        toolCallId
    }

    var displayText: UiVerbatimText {
        switch result {
        case .null: .protocolValue("null")
        case let .bool(value): .verbatim(String(value))
        case let .number(value): .verbatim(String(value))
        case let .string(value): .verbatim(value)
        case let .array(value): .count(value.count, item: "item")
        case let .object(value): .count(value.count, item: "field")
        }
    }
}
