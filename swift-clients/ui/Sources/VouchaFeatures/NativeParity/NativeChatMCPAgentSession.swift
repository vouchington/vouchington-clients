import Foundation
import Observation
import VouchaAPI
import VouchaLocalization
import VouchaModels

/// Keeps an agent's MCP grant and discovered tools scoped to the signed-in member.
@MainActor
@Observable
public final class NativeChatMCPAgentSession {
    public struct ConnectedSnapshot: Sendable {
        public let accountId: String
        public let revision: Int
        public let catalog: DecodedJSONValue
    }

    public enum Status: Equatable {
        case disconnected
        case connecting
        case connected
        case switchingAccount
        case failed
    }

    public private(set) var accountId: String?
    public private(set) var status: Status = .disconnected
    public private(set) var toolCatalog: DecodedJSONValue?
    public private(set) var lastError: UiVerbatimText?
    public private(set) var revision = 0

    let service: any NativeChatMCPServicing
    private var generation = 0
    private var operation: Task<DecodedJSONValue, Error>?
    private var pendingCleanupAccountIds: [String] = []
    var inFlightCalls: [UUID: Task<MemberMCPClient.ToolResult, Error>] = [:]

    public init(service: any NativeChatMCPServicing) {
        self.service = service
    }

    public func connectedSnapshot() -> ConnectedSnapshot? {
        guard status == .connected, let accountId, let toolCatalog else { return nil }
        return .init(accountId: accountId, revision: revision, catalog: toolCatalog)
    }

    public func isCurrent(_ snapshot: ConnectedSnapshot) -> Bool {
        status == .connected && accountId == snapshot.accountId && revision == snapshot.revision
    }

    public func useAccount(_ nextAccountId: String?) async {
        guard nextAccountId != accountId || !pendingCleanupAccountIds.isEmpty else { return }
        generation += 1
        revision += 1
        let currentGeneration = generation
        operation?.cancel()
        operation = nil
        cancelCalls()
        let previousAccountId = accountId
        accountId = nextAccountId
        toolCatalog = nil
        lastError = nil
        if let previousAccountId { enqueueCleanup(previousAccountId) }
        status = pendingCleanupAccountIds.isEmpty ? .disconnected : .switchingAccount
        do {
            try await drainPendingCleanup()
        } catch {
            guard generation == currentGeneration else { return }
            lastError = .verbatim(error.localizedDescription)
            status = .failed
            return
        }
        guard generation == currentGeneration else { return }
        status = .disconnected
    }

    public func connect(presentationWindow: AnyObject? = nil) async throws {
        guard let accountId, status != .switchingAccount, status != .connecting else { return }
        generation += 1
        revision += 1
        let currentGeneration = generation
        operation?.cancel()
        cancelCalls()
        status = .connecting
        lastError = nil
        let task = Task {
            try await drainPendingCleanup()
            try Task.checkCancellation()
            return try await service.connect(accountId: accountId, presentationWindow: presentationWindow)
        }
        operation = task
        do {
            let catalog = try await task.value
            guard generation == currentGeneration, self.accountId == accountId else { return }
            toolCatalog = catalog
            lastError = nil
            status = .connected
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

    public func refreshTools() async throws {
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
            toolCatalog = catalog
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

    public func disconnect() async throws {
        guard let accountId else { return }
        generation += 1
        revision += 1
        let currentGeneration = generation
        operation?.cancel()
        operation = nil
        cancelCalls()
        toolCatalog = nil
        lastError = nil
        status = .switchingAccount
        enqueueCleanup(accountId)
        do {
            try await drainPendingCleanup()
            guard generation == currentGeneration, self.accountId == accountId else { return }
            status = .disconnected
        } catch {
            guard generation == currentGeneration, self.accountId == accountId else { return }
            status = .failed
            lastError = .verbatim(error.localizedDescription)
            throw error
        }
    }

    private func enqueueCleanup(_ accountId: String) {
        if !pendingCleanupAccountIds.contains(accountId) {
            pendingCleanupAccountIds.append(accountId)
        }
    }

    private func drainPendingCleanup() async throws {
        while let accountId = pendingCleanupAccountIds.first {
            try await service.disconnect(accountId: accountId)
            pendingCleanupAccountIds.removeAll { $0 == accountId }
        }
    }

}

public extension NativeChatMCPAgentSession {
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
