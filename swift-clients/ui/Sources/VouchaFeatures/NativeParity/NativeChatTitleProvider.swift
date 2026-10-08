import Foundation
import VouchaLocalization

enum NativeChatTitleProviderKind: Hashable, Identifiable {
    case appleFoundationModels
    case openAICompatible(endpointID: UUID)
    /// On-device generation via Android's system AICore. Not reachable from `ui/`'s
    /// `VouchaFeatures` package today — see `NativeChatTitleProvider+AndroidAICore.swift` — but
    /// modeled as its own case so persisted selections and the backend's client-generated-chat
    /// contract have a stable identity ahead of #6745.
    case androidAICore
    case unavailable(id: String)

    var id: String {
        switch self {
        case .appleFoundationModels:
            "apple_foundation"
        case let .openAICompatible(endpointID):
            "openai_compatible:\(endpointID.uuidString.lowercased())"
        case .androidAICore:
            "android_aicore"
        case let .unavailable(id):
            id
        }
    }

    init(persistedID: String) {
        switch persistedID {
        case "apple_foundation":
            self = .appleFoundationModels
        case "android_aicore", "aiCore":
            self = .androidAICore
        default:
            let prefix = "openai_compatible:"
            if persistedID.hasPrefix(prefix),
               let endpointID = UUID(uuidString: String(persistedID.dropFirst(prefix.count))) {
                self = .openAICompatible(endpointID: endpointID)
            } else {
                self = .unavailable(id: persistedID)
            }
        }
    }

    /// `.androidAICore` uses the existing Android-shell label so a fallback-appended persisted
    /// row cannot collide with `.appleFoundationModels`'s "Local" label.
    var displayName: UiVerbatimText {
        switch self {
        case .appleFoundationModels:
            .message(.nativeSwiftChatLocal)
        case .openAICompatible:
            .message(.nativeSwiftChatLocal)
        case .androidAICore:
            .message(.nativeSwiftAndroidProviderAicore)
        case let .unavailable(id):
            .message(.nativeSwiftRouteSurfaceProviderUnavailable, parameters: ["provider": id])
        }
    }

    /// `.androidAICore` joins `.unavailable`'s arm here (unlike `displayName` above): this text
    /// renders directly from `viewModel.titleProviderSelection.detailText` whenever a persisted
    /// `"android_aicore"` selection round-trips back to `.androidAICore` — including on platforms
    /// where it's excluded from the picker — so it must describe unavailability, not just identity.
    var detailText: UiMessage {
        switch self {
        case .appleFoundationModels:
            UiMessage(.nativeSwiftChatLocal)
        case .openAICompatible:
            UiMessage(.nativeSwiftChatOpenAiCompatibleResponsesApi)
        case .androidAICore, .unavailable:
            UiMessage(.nativeSwiftChatLocalModelsUnavailablePlatform)
        }
    }

}

struct NativeChatProviderDescriptor: Equatable, Identifiable {
    let selection: NativeChatTitleProviderKind
    let displayName: UiVerbatimText
    let status: NativeChatTitleProviderStatus

    var id: String {
        selection.id
    }
}

struct NativeChatTitleProviderStatus: Equatable {
    let isAvailable: Bool
    let detail: UiVerbatimText?
}

struct NativeChatAssistantResponse: Equatable {
    let content: String
    let modelProvider: String
    let modelName: String?
}

protocol NativeChatTitleProviding: Sendable {
    var status: NativeChatTitleProviderStatus { get }

    func generateAssistantResponse(
        to message: String,
        history: [NativeChatTimelineMessage]
    ) async throws -> NativeChatAssistantResponse?

    func generateTitle(from messages: [NativeChatTimelineMessage]) async throws -> String?
}

protocol NativeChatTitleProviderResolving: Sendable {
    func defaultSelection() -> NativeChatTitleProviderKind
    func provider(for kind: NativeChatTitleProviderKind) -> any NativeChatTitleProviding
    func providerDescriptors() -> [NativeChatProviderDescriptor]
    func persistSelection(_ kind: NativeChatTitleProviderKind) async -> Bool
}

extension NativeChatTitleProviderResolving {
    func providerDescriptors() -> [NativeChatProviderDescriptor] {
        []
    }

    func persistSelection(_: NativeChatTitleProviderKind) async -> Bool {
        true
    }
}
