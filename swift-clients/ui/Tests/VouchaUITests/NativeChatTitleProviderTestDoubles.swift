@testable import VouchaFeatures

final class StubTitleProvider: NativeChatTitleProviding, @unchecked Sendable {
    let kind: NativeChatTitleProviderKind
    let status: NativeChatTitleProviderStatus
    let assistantResponse: String?
    let title: String?
    let clientGeneratedModelProvider: String?
    let clientGeneratedModelName: String?
    let generatedModelProvider: String?
    let generatedModelName: String?
    private(set) var generateAssistantResponseCallCount = 0
    private(set) var generateTitleCallCount = 0

    init(
        kind: NativeChatTitleProviderKind,
        status: NativeChatTitleProviderStatus? = nil,
        assistantResponse: String? = nil,
        title: String?,
        clientGeneratedModelProvider: String? = nil,
        clientGeneratedModelName: String? = nil,
        generatedModelProvider: String? = nil,
        generatedModelName: String? = nil
    ) {
        self.kind = kind
        self.assistantResponse = assistantResponse
        self.title = title
        self.clientGeneratedModelProvider = clientGeneratedModelProvider ?? Self.defaultModelProvider(for: kind)
        self.clientGeneratedModelName = clientGeneratedModelName ?? Self.defaultModelName(for: kind)
        self.generatedModelProvider = generatedModelProvider
        self.generatedModelName = generatedModelName
        self.status = status ?? .init(isAvailable: kind.isLocal, detail: nil)
    }

    func generateAssistantResponse(
        to _: String,
        history _: [NativeChatTimelineMessage]
    ) async throws -> NativeChatAssistantResponse? {
        generateAssistantResponseCallCount += 1
        return assistantResponse.map {
            NativeChatAssistantResponse(
                content: $0,
                modelProvider: generatedModelProvider
                    ?? clientGeneratedModelProvider
                    ?? Self.defaultModelProvider(for: kind)
                    ?? "apple_foundation",
                modelName: generatedModelName ?? clientGeneratedModelName
            )
        }
    }

    func generateTitle(from _: [NativeChatTimelineMessage]) async throws -> String? {
        generateTitleCallCount += 1
        return title
    }

    /// Per-kind, not blanket-`isLocal`: `.openAICompatible` and `.androidAICore` are both
    /// `isLocal == true` but must not silently default to Apple's identity.
    private static func defaultModelProvider(for kind: NativeChatTitleProviderKind) -> String? {
        switch kind {
        case .appleFoundationModels:
            "apple_foundation"
        case .androidAICore:
            "android_aicore"
        case .openAICompatible, .openAI, .anthropic, .unavailable:
            // Tombstone and hosted kinds never persist a client-generated identity.
            nil
        }
    }

    private static func defaultModelName(for kind: NativeChatTitleProviderKind) -> String? {
        switch kind {
        case .appleFoundationModels:
            "apple-foundation-system"
        case .androidAICore:
            "android-aicore-system"
        case .openAICompatible, .openAI, .anthropic, .unavailable:
            nil
        }
    }
}

struct StubTitleProviderResolver: NativeChatTitleProviderResolving, @unchecked Sendable {
    let defaultSelectionValue: NativeChatTitleProviderKind
    let providers: [NativeChatTitleProviderKind: any NativeChatTitleProviding]

    func defaultSelection() -> NativeChatTitleProviderKind {
        defaultSelectionValue
    }

    func provider(for kind: NativeChatTitleProviderKind) -> any NativeChatTitleProviding {
        providers[kind] ?? providers[defaultSelectionValue] ?? NativeChatHostedTitleProvider(kind: kind)
    }
}
