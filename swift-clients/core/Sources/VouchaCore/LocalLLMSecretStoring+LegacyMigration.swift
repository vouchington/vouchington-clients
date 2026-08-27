import Foundation

public extension LocalLLMSecretStoring {
    func migrateLegacyAPIKey(to _: UUID) async throws {}
    func clearLegacyAPIKey() async throws {}
}
