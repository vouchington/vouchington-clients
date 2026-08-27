import Foundation
import VouchaCore
import VouchaLocalization

#if canImport(FoundationModels)
    import FoundationModels
#endif

struct NativeChatAppleFoundationModelsProvider: NativeChatTitleProviding {
    static let maximumAssistantPromptCharacters = 12_000
    private static let assistantInstruction =
        "You are Voucha's chat assistant. Answer the latest user message using the conversation context."
    private static let latestMessageHeading = "\n\nLatest user message:\n"
    private let featurePolicy: LocalLLMFeaturePolicy

    init(featurePolicy: LocalLLMFeaturePolicy = LocalLLMFeaturePolicy()) {
        self.featurePolicy = featurePolicy
    }

    var status: NativeChatTitleProviderStatus {
        guard featurePolicy.isEnabled else {
            return .init(isAvailable: false, detail: .message(.nativeSwiftChatLocalModelsUnavailablePlatform))
        }
        #if canImport(FoundationModels)
            if #available(macOS 26.0, iOS 26.0, *) {
                switch SystemLanguageModel.default.availability {
                case .available:
                    return .init(isAvailable: true, detail: .message(.nativeSwiftChatLocal))
                case .unavailable:
                    return .init(isAvailable: false, detail: .message(.nativeSwiftChatOnDeviceUnavailable))
                }
            }
        #endif
        return .init(isAvailable: false, detail: .message(.nativeSwiftChatLocalModelsUnavailablePlatform))
    }

    func generateAssistantResponse(
        to message: String,
        history: [NativeChatTimelineMessage]
    ) async throws -> NativeChatAssistantResponse? {
        #if canImport(FoundationModels)
            guard #available(macOS 26.0, iOS 26.0, *), status.isAvailable else { return nil }
            let session = LanguageModelSession(model: SystemLanguageModel.default)
            let response = try await session.respond(to: Self.assistantPrompt(message: message, history: history))
            let content = response.content.trimmingCharacters(in: .whitespacesAndNewlines)
            guard !content.isEmpty else { return nil }
            return NativeChatAssistantResponse(
                content: content,
                modelProvider: "apple_foundation",
                modelName: "apple-foundation-system"
            )
        #else
            return nil
        #endif
    }

    func generateTitle(from messages: [NativeChatTimelineMessage]) async throws -> String? {
        #if canImport(FoundationModels)
            guard #available(macOS 26.0, iOS 26.0, *), status.isAvailable else { return nil }
            let transcript = Self.boundedTranscript(
                messages,
                maximumCharacters: Self.maximumAssistantPromptCharacters - 72,
                maximumMessages: 8
            )
            let session = LanguageModelSession(model: SystemLanguageModel.default)
            let response = try await session.respond(to: """
            Suggest a concise conversation title of six words or fewer.
            Return only the title.

            \(transcript)
            """)
            return sanitizeTitle(response.content)
        #else
            return nil
        #endif
    }

    static func assistantPrompt(
        message: String,
        history: [NativeChatTimelineMessage],
        maximumCharacters: Int = maximumAssistantPromptCharacters
    ) -> String {
        let fixedCharacters = assistantInstruction.count + 2 + latestMessageHeading.count
        let contentBudget = max(0, maximumCharacters - fixedCharacters)
        let latestMessage = String(message.trimmingCharacters(in: .whitespacesAndNewlines).prefix(contentBudget))
        let transcriptBudget = max(0, contentBudget - latestMessage.count)
        let transcript = boundedTranscript(history, maximumCharacters: transcriptBudget, maximumMessages: 12)
        return "\(assistantInstruction)\n\n\(transcript)\(latestMessageHeading)\(latestMessage)"
    }

    private static func boundedTranscript(
        _ messages: [NativeChatTimelineMessage],
        maximumCharacters: Int,
        maximumMessages: Int
    ) -> String {
        guard maximumCharacters > 0 else { return "" }
        var remaining = maximumCharacters
        var selected: [String] = []
        for message in (messages
            .filter { !$0.isStreaming && !$0.content.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
            .suffix(maximumMessages)
            .reversed()) {
            let separatorLength = selected.isEmpty ? 0 : 1
            let available = remaining - separatorLength
            guard available > 0 else { break }
            let line = chatTranscriptLine(message)
            selected.append(String(line.prefix(available)))
            remaining -= separatorLength + min(line.count, available)
        }
        return selected.reversed().joined(separator: "\n")
    }

    private static func chatTranscriptLine(_ message: NativeChatTimelineMessage) -> String {
        "\(message.role.rawValue): \(message.content.trimmingCharacters(in: .whitespacesAndNewlines))"
    }

    private func sanitizeTitle(_ title: String) -> String? {
        let cleaned = title
            .trimmingCharacters(in: .whitespacesAndNewlines)
            .replacingOccurrences(of: "\"", with: "")
        return cleaned.isEmpty ? nil : cleaned
    }
}
