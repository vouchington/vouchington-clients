import Foundation
import SkipFuse
import SkipKeychain
import VouchaCore
import VouchaLocalization

enum AndroidModelState: String {
    case checking, unavailable, downloadable, downloading, available
}

protocol AndroidAICoreRunning: Sendable {
    func status() async throws -> AndroidModelState
    func download() async throws -> AndroidModelState
    func generate(prompt: String) async throws -> String
}

protocol AndroidEndpointSecretStoring {
    func string(forKey key: String) throws -> String?
    func set(_ value: String, forKey key: String) throws
    func removeValue(forKey key: String) throws
}

struct SkipKeychainEndpointSecretStore: AndroidEndpointSecretStoring {
    private let keychain: Keychain

    init(keychain: Keychain = .shared) {
        self.keychain = keychain
    }

    func string(forKey key: String) throws -> String? {
        try keychain.string(forKey: key)
    }

    func set(_ value: String, forKey key: String) throws {
        try keychain.set(value, forKey: key)
    }

    func removeValue(forKey key: String) throws {
        try keychain.removeValue(forKey: key)
    }
}

struct AndroidAICoreRuntime: AndroidAICoreRunning {
    func status() async throws -> AndroidModelState {
        try await call { try $0.status() }
    }

    func download() async throws -> AndroidModelState {
        try await call { try $0.download() }
    }

    func generate(prompt: String) async throws -> String {
        #if os(Android)
            return try await Task.detached {
                let bridge = try D.voucha.android.AICoreBridge.INSTANCE()
                let response: String = try bridge.generate(prompt) ?? ""
                return response.trimmingCharacters(in: .whitespacesAndNewlines)
            }.value
        #else
            throw AndroidAICoreError.unsupportedPlatform
        #endif
    }

    private func call(
        _ operation: @escaping @Sendable (AnyDynamicObject) throws -> String?
    ) async throws -> AndroidModelState {
        #if os(Android)
            let value = try await Task.detached {
                try operation(D.voucha.android.AICoreBridge.INSTANCE()) ?? "unavailable"
            }.value
            if value.hasPrefix("error:") {
                throw AndroidAICoreError.bridgeFailure(String(value.dropFirst("error:".count)))
            }
            return AndroidModelState(rawValue: value) ?? .unavailable
        #else
            throw AndroidAICoreError.unsupportedPlatform
        #endif
    }
}

enum AndroidAICoreError: LocalizedError {
    case bridgeFailure(String)
    case emptyResponse
    case endpointSaveFailed
    case unsupportedPlatform

    var errorDescription: String? {
        switch self {
        case let .bridgeFailure(message):
            message
        case .emptyResponse:
            UiMessages.string(.nativeSwiftAndroidEmptyResponse, locale: .current)
        case .endpointSaveFailed:
            UiMessages.string(.nativeSwiftAndroidEndpointSaveFailed, locale: .current)
        case .unsupportedPlatform:
            UiMessages.string(.nativeSwiftAndroidAicoreUnavailable, locale: .current)
        }
    }
}

struct AndroidChatMessage: Identifiable, Equatable {
    let id = UUID()
    let role: String
    let content: String
}

protocol AndroidEndpointGenerating: Sendable {
    func generateAssistantResponse(
        message: String,
        history: [LocalLLMChatMessage],
        endpoint: LocalLLMEndpointProfile,
        apiKey: String?
    ) async throws -> String?
}

extension OpenAICompatibleResponsesClient: AndroidEndpointGenerating {}
