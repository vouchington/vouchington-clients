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
        #if os(Android)
            try D.voucha.android.CookieSnapshotBridge.INSTANCE().stringValue(key)
        #else
            try keychain.string(forKey: key)
        #endif
    }

    func set(_ value: String, forKey key: String) throws {
        #if os(Android)
            let _: Void = try D.voucha.android.CookieSnapshotBridge.INSTANCE().setValue(key, value)
        #else
            try keychain.set(value, forKey: key)
        #endif
    }

    func removeValue(forKey key: String) throws {
        #if os(Android)
            let _: Void = try D.voucha.android.CookieSnapshotBridge.INSTANCE().removeValue(key)
        #else
            try keychain.removeValue(forKey: key)
        #endif
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
            let requestID = UUID().uuidString
            let _: Void = try D.voucha.android.AICoreBridge.INSTANCE().prepareGeneration(requestID)
            defer { let _: Void? = try? D.voucha.android.AICoreBridge.INSTANCE().finishGeneration(requestID) }
            return try await withTaskCancellationHandler {
                try Task.checkCancellation()
                return try await Task.detached {
                    let bridge = try D.voucha.android.AICoreBridge.INSTANCE()
                    let response: String = try bridge.generate(prompt, requestID) ?? ""
                    return response.trimmingCharacters(in: .whitespacesAndNewlines)
                }.value
            } onCancel: {
                let _: Void? = try? D.voucha.android.AICoreBridge.INSTANCE().cancelGeneration(requestID)
            }
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

protocol AndroidEndpointGenerating: Sendable {
    func generateAssistantResponse(
        message: String,
        history: [LocalLLMChatMessage],
        endpoint: LocalLLMEndpointProfile,
        apiKey: String?
    ) async throws -> String?
}

extension OpenAICompatibleResponsesClient: AndroidEndpointGenerating {}
