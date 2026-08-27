import Foundation

public protocol LocalLLMSecretStoring: Sendable {
    func readAPIKey(for endpointID: UUID) async -> String?
    func saveAPIKey(_ apiKey: String, for endpointID: UUID) async throws
    func clearAPIKey(for endpointID: UUID) async throws
    func migrateLegacyAPIKey(to endpointID: UUID) async throws
    func clearLegacyAPIKey() async throws
}

public actor InMemoryLocalLLMSecretStore: LocalLLMSecretStoring {
    var apiKeys: [UUID: String]
    var legacyAPIKey: String?

    public init(apiKeys: [UUID: String] = [:], legacyAPIKey: String? = nil) {
        self.apiKeys = apiKeys
        self.legacyAPIKey = legacyAPIKey
    }

    public func readAPIKey(for endpointID: UUID) async -> String? {
        apiKeys[endpointID]
    }

    public func saveAPIKey(_ apiKey: String, for endpointID: UUID) async {
        apiKeys[endpointID] = apiKey
    }

    public func clearAPIKey(for endpointID: UUID) async {
        apiKeys[endpointID] = nil
    }

}

public final class LocalLLMSettingsStore: @unchecked Sendable {
    static let legacyEndpointID = UUID(uuid: (
        0x70, 0xCD, 0x23, 0x7F, 0x69, 0x20, 0x4A, 0x4A,
        0x81, 0xDE, 0x03, 0xDA, 0xD9, 0xAF, 0xBA, 0xB6
    ))

    let fileURL: URL
    let secretStore: any LocalLLMSecretStoring
    let configurationWriter: @Sendable (URL, LocalLLMConfiguration) -> Bool
    let configurationRemover: @Sendable (URL) -> Bool
    let configurationReader: @Sendable (URL) -> LocalLLMConfigurationReadResult

    public convenience init(
        fileURL: URL = LocalLLMSettingsStore.defaultFileURL(),
        secretStore: any LocalLLMSecretStoring = KeychainLocalLLMSecretStore()
    ) {
        self.init(
            fileURL: fileURL,
            secretStore: secretStore,
            configurationWriter: { LocalLLMSettingsStore.write($1, to: $0) },
            configurationRemover: { LocalLLMSettingsStore.remove(at: $0) },
            configurationReader: { LocalLLMSettingsStore.read(from: $0) }
        )
    }

    init(
        fileURL: URL,
        secretStore: any LocalLLMSecretStoring,
        configurationWriter: @escaping @Sendable (URL, LocalLLMConfiguration) -> Bool,
        configurationRemover: @escaping @Sendable (URL) -> Bool = { LocalLLMSettingsStore.remove(at: $0) },
        configurationReader: @escaping @Sendable (URL) -> LocalLLMConfigurationReadResult = {
            LocalLLMSettingsStore.read(from: $0)
        }
    ) {
        self.fileURL = fileURL.standardizedFileURL
        self.secretStore = secretStore
        self.configurationWriter = configurationWriter
        self.configurationRemover = configurationRemover
        self.configurationReader = configurationReader
    }

    public func load() -> LocalLLMConfiguration {
        guard case let .data(data) = configurationReader(fileURL) else {
            return .disabled
        }
        if let configuration = try? JSONDecoder().decode(LocalLLMConfiguration.self, from: data) {
            return configuration
        }
        guard let legacy = try? JSONDecoder().decode(LegacyLocalLLMConfiguration.self, from: data) else {
            return .disabled
        }
        let configuration = legacy.migratedConfiguration(endpointID: Self.legacyEndpointID)
        guard write(configuration) else { return configuration }
        scheduleLegacySecretMigration()
        return configuration
    }

    public func save(
        _ configuration: LocalLLMConfiguration,
        apiKeys: [UUID: String] = [:]
    ) async -> Bool {
        await enqueue { [self] in
            await persist(configuration, apiKeys: apiKeys)
        }
    }

    public func update(
        apiKeys: [UUID: String] = [:],
        _ mutation: @escaping @Sendable (inout LocalLLMConfiguration) -> Void
    ) async -> Bool {
        await enqueue { [self] in
            guard let previous = loadForMutation() else { return false }
            var configuration = previous
            mutation(&configuration)
            return await persist(configuration, apiKeys: apiKeys, previous: previous)
        }
    }

    private func loadForMutation() -> LocalLLMConfiguration? {
        let readResult = configurationReader(fileURL)
        guard case let .data(data) = readResult else {
            return readResult.isMissing ? .disabled : nil
        }
        if let configuration = try? JSONDecoder().decode(LocalLLMConfiguration.self, from: data) {
            return configuration
        }
        guard let legacy = try? JSONDecoder().decode(LegacyLocalLLMConfiguration.self, from: data) else {
            return nil
        }
        let configuration = legacy.migratedConfiguration(endpointID: Self.legacyEndpointID)
        guard write(configuration) else { return nil }
        scheduleLegacySecretMigration()
        return configuration
    }

    public func readAPIKey(for endpointID: UUID) async -> String? {
        if endpointID == Self.legacyEndpointID,
           load().endpoint(id: endpointID) != nil {
            await migrateLegacySecret()
        }
        return await secretStore.readAPIKey(for: endpointID)
    }

    public func clear() async -> Bool {
        await enqueue { [self] in
            let configuration = load()
            let endpointIDs = Set(configuration.endpoints.map(\.id))
            guard write(configuration.safeBeforeSecretReplacement(for: endpointIDs)) else { return false }
            do {
                for endpoint in configuration.endpoints {
                    try await secretStore.clearAPIKey(for: endpoint.id)
                }
                try await secretStore.clearLegacyAPIKey()
            } catch {
                return false
            }
            return configurationRemover(fileURL)
        }
    }

    public static func defaultFileURL(fileManager: FileManager = .default) -> URL {
        let base = fileManager.urls(for: .applicationSupportDirectory, in: .userDomainMask).first
            ?? fileManager.temporaryDirectory
        return base
            .appendingPathComponent("ai.voucha", isDirectory: true)
            .appendingPathComponent("local-llm-settings.json")
    }

}

public struct LocalLLMFeaturePolicy: Sendable {
    public let isEnabled: Bool

    public init(environment: [String: String] = ProcessInfo.processInfo.environment) {
        if let override = environment["VOUCHA_NATIVE_LOCAL_LLM_ENABLED"]?
            .trimmingCharacters(in: .whitespacesAndNewlines)
            .lowercased(), !override.isEmpty {
            isEnabled = override == "1" || override == "true" || override == "yes"
            return
        }
        #if os(macOS) || os(iOS) || os(Android)
            isEnabled = true
        #else
            isEnabled = false
        #endif
    }
}

public struct LocalLLMChatMessage: Sendable, Equatable {
    public let role: String
    public let content: String

    public init(role: String, content: String) {
        self.role = role
        self.content = content
    }
}
