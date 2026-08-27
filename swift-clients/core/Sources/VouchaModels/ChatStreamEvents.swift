import Foundation

public enum ChatStreamEvent: Sendable {
    case metadata(ChatStreamMetadata)
    case text(String)
    case message(String)
    case toolCall(ChatStreamToolCall)
    case toolResult(toolCallId: String, result: DecodedJSONValue)
    case subagentStep(ChatStreamSubagentStep)
    case subagentText(ChatStreamSubagentText)
    case error(String)
    case done

    public init?(eventType: String, rawData: String) {
        guard let event = ChatStreamEventPayloadFactory.event(eventType: eventType, rawData: rawData) else {
            return nil
        }
        self = event
    }
}

private enum ChatStreamEventPayloadFactory {
    private typealias EventBuilder = (String, JSONDecoder) -> ChatStreamEvent?

    private static let builders: [String: EventBuilder] = [
        "metadata": metadataEvent,
        "text": textEvent,
        "message": messageEvent,
        "tool_call": toolCallEvent,
        "tool_result": toolResultEvent,
        "subagent_step": subagentStepEvent,
        "subagent_text": subagentTextEvent,
        "error": errorEvent,
        "done": doneEvent
    ]

    static func event(eventType: String, rawData: String) -> ChatStreamEvent? {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        return builders[eventType]?(rawData, decoder)
    }

    private static func decode<T: Decodable>(_ type: T.Type, rawData: String, decoder: JSONDecoder) -> T? {
        try? decoder.decode(type, from: Data(rawData.utf8))
    }

    private static func metadataEvent(rawData: String, decoder: JSONDecoder) -> ChatStreamEvent? {
        guard let payload = decode(ChatStreamMetadataPayload.self, rawData: rawData, decoder: decoder)
        else { return nil }
        return .metadata(
            .init(
                conversationId: payload.conversationId,
                userMessageId: payload.userMessageId,
                assistantMessageId: payload.assistantMessageId,
                jobId: payload.jobId
            )
        )
    }

    private static func textEvent(rawData: String, decoder: JSONDecoder) -> ChatStreamEvent? {
        guard let payload = decode(ChatStreamTextPayload.self, rawData: rawData, decoder: decoder) else { return nil }
        return .text(payload.content)
    }

    private static func messageEvent(rawData: String, decoder: JSONDecoder) -> ChatStreamEvent? {
        guard let payload = decode(ChatStreamTextPayload.self, rawData: rawData, decoder: decoder) else { return nil }
        return .message(payload.content)
    }

    private static func toolCallEvent(rawData: String, decoder: JSONDecoder) -> ChatStreamEvent? {
        guard let payload = decode(ChatStreamToolCallPayload.self, rawData: rawData, decoder: decoder)
        else { return nil }
        return .toolCall(.init(toolCallId: payload.toolCallId, name: payload.name, arguments: payload.arguments))
    }

    private static func toolResultEvent(rawData: String, decoder: JSONDecoder) -> ChatStreamEvent? {
        guard let payload = decode(ChatStreamToolResultPayload.self, rawData: rawData, decoder: decoder)
        else { return nil }
        return .toolResult(toolCallId: payload.toolCallId, result: payload.result)
    }

    private static func subagentStepEvent(rawData: String, decoder: JSONDecoder) -> ChatStreamEvent? {
        guard let payload = decode(ChatStreamSubagentStepPayload.self, rawData: rawData, decoder: decoder) else {
            return nil
        }
        return .subagentStep(.init(
            agentName: payload.agentName,
            toolName: payload.toolName,
            toolCallId: payload.toolCallId
        ))
    }

    private static func subagentTextEvent(rawData: String, decoder: JSONDecoder) -> ChatStreamEvent? {
        guard let payload = decode(ChatStreamSubagentTextPayload.self, rawData: rawData, decoder: decoder) else {
            return nil
        }
        return .subagentText(.init(
            agentName: payload.agentName,
            toolCallId: payload.toolCallId,
            content: payload.content
        ))
    }

    private static func errorEvent(rawData: String, decoder: JSONDecoder) -> ChatStreamEvent? {
        guard let payload = decode(ChatStreamErrorPayload.self, rawData: rawData, decoder: decoder) else { return nil }
        return .error(payload.error)
    }

    private static func doneEvent(rawData _: String, decoder _: JSONDecoder) -> ChatStreamEvent? {
        .done
    }
}

public struct ChatStreamMetadata: Sendable {
    public let conversationId: String
    public let userMessageId: String
    public let assistantMessageId: String
    public let jobId: String

    public init(conversationId: String, userMessageId: String, assistantMessageId: String, jobId: String) {
        self.conversationId = conversationId
        self.userMessageId = userMessageId
        self.assistantMessageId = assistantMessageId
        self.jobId = jobId
    }
}

public struct ChatStreamToolCall: Sendable {
    public let toolCallId: String
    public let name: String
    public let arguments: String

    public init(toolCallId: String, name: String, arguments: String) {
        self.toolCallId = toolCallId
        self.name = name
        self.arguments = arguments
    }
}

public struct ChatStreamSubagentStep: Sendable {
    public let agentName: String
    public let toolName: String
    public let toolCallId: String?

    public init(agentName: String, toolName: String, toolCallId: String?) {
        self.agentName = agentName
        self.toolName = toolName
        self.toolCallId = toolCallId
    }
}

public struct ChatStreamSubagentText: Sendable {
    public let agentName: String
    public let toolCallId: String?
    public let content: String

    public init(agentName: String, toolCallId: String?, content: String) {
        self.agentName = agentName
        self.toolCallId = toolCallId
        self.content = content
    }
}

private struct ChatStreamMetadataPayload: Decodable {
    let conversationId: String
    let userMessageId: String
    let assistantMessageId: String
    let jobId: String
}

private struct ChatStreamTextPayload: Decodable {
    let content: String
}

private struct ChatStreamToolCallPayload: Decodable {
    let toolCallId: String
    let name: String
    let arguments: String
}

private struct ChatStreamToolResultPayload: Decodable {
    let toolCallId: String
    let result: DecodedJSONValue
}

private struct ChatStreamSubagentStepPayload: Decodable {
    let agentName: String
    let toolName: String
    let toolCallId: String?
}

private struct ChatStreamSubagentTextPayload: Decodable {
    let agentName: String
    let toolCallId: String?
    let content: String
}

private struct ChatStreamErrorPayload: Decodable {
    let error: String
}
