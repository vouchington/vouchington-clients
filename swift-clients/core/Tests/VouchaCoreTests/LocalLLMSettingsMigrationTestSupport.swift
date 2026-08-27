import Foundation
@testable import VouchaCore

actor DelayedLegacySecretStore: LocalLLMSecretStoring {
    private var apiKeys: [UUID: String] = [:]
    private var legacyAPIKey: String?
    private var migrationStarted = false
    private var migrationWaiter: CheckedContinuation<Void, Never>?
    private var migrationRelease: CheckedContinuation<Void, Never>?

    init(legacyAPIKey: String) {
        self.legacyAPIKey = legacyAPIKey
    }

    func readAPIKey(for endpointID: UUID) async -> String? {
        apiKeys[endpointID]
    }

    func saveAPIKey(_ apiKey: String, for endpointID: UUID) async {
        apiKeys[endpointID] = apiKey
    }

    func clearAPIKey(for endpointID: UUID) async {
        apiKeys[endpointID] = nil
    }

    func migrateLegacyAPIKey(to endpointID: UUID) async {
        migrationStarted = true
        migrationWaiter?.resume()
        migrationWaiter = nil
        await withCheckedContinuation { migrationRelease = $0 }
        if apiKeys[endpointID] == nil {
            apiKeys[endpointID] = legacyAPIKey
        }
        legacyAPIKey = nil
    }

    func clearLegacyAPIKey() async {
        legacyAPIKey = nil
    }

    func readLegacyAPIKey() -> String? {
        legacyAPIKey
    }

    func waitUntilMigrationStarts() async {
        guard !migrationStarted else { return }
        await withCheckedContinuation { migrationWaiter = $0 }
    }

    func releaseMigration() {
        migrationRelease?.resume()
        migrationRelease = nil
    }
}
