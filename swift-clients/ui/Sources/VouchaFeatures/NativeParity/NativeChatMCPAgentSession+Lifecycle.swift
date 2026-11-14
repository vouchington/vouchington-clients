import Foundation
import VouchaModels

public extension NativeChatMCPAgentSession {
    func refreshTools() async throws {
        guard let accountId, status == .connected else { return }
        generation += 1
        revision += 1
        let currentGeneration = generation
        operation?.cancel()
        cancelCalls()
        toolCatalog = nil
        let task = Task { try await service.refreshTools(accountId: accountId) }
        operation = task
        do {
            let catalog = try await task.value
            guard generation == currentGeneration, self.accountId == accountId else { return }
            toolCatalog = try NativeChatMCPToolCatalog.validatedCapacity(catalog)
            lastError = nil
            operation = nil
        } catch {
            guard generation == currentGeneration, self.accountId == accountId else { return }
            toolCatalog = nil
            lastError = .verbatim(error.localizedDescription)
            status = .failed
            operation = nil
            throw error
        }
    }

    func connectedSnapshot() -> ConnectedSnapshot? {
        guard status == .connected, let accountId, let toolCatalog else { return nil }
        return .init(accountId: accountId, revision: revision, catalog: toolCatalog)
    }

    func isCurrent(_ snapshot: ConnectedSnapshot) -> Bool {
        status == .connected && accountId == snapshot.accountId && revision == snapshot.revision
    }

    /// Completes local grant removal while the app still knows which member is signing out.
    func prepareForSignOut(accountId signingOutAccountId: String) async throws {
        generation += 1
        revision += 1
        let currentGeneration = generation
        operation?.cancel()
        operation = nil
        cancelCalls()
        if let accountId { enqueueCleanup(accountId) }
        enqueueCleanup(signingOutAccountId)
        requestWriteAccess = false
        accountId = nil
        toolCatalog = nil
        lastError = nil
        status = .switchingAccount
        do {
            try await drainPendingCleanup()
            guard generation == currentGeneration else { return }
            status = .disconnected
        } catch {
            guard generation == currentGeneration else { throw error }
            status = .failed
            lastError = .verbatim(error.localizedDescription)
            throw error
        }
    }
}
