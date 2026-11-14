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

    public var requestWriteAccess = false
    public internal(set) var accountId: String?
    public internal(set) var status: Status = .disconnected
    public internal(set) var toolCatalog: DecodedJSONValue?
    public internal(set) var lastError: UiVerbatimText?
    public internal(set) var revision = 0

    let service: any NativeChatMCPServicing
    var generation = 0
    var operation: Task<DecodedJSONValue, Error>?
    private var pendingCleanupAccountIds: [String] = []
    var inFlightCalls: [UUID: Task<MemberMCPClient.ToolResult, Error>] = [:]

    public init(service: any NativeChatMCPServicing) {
        self.service = service
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
        requestWriteAccess = false
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

    public func connect(presentationWindow: AnyObject? = nil, scope: String? = nil) async throws {
        guard let accountId, status != .switchingAccount, status != .connecting else { return }
        generation += 1
        revision += 1
        let currentGeneration = generation
        operation?.cancel()
        cancelCalls()
        status = .connecting
        toolCatalog = nil
        lastError = nil
        let task = Task {
            try await drainPendingCleanup()
            try Task.checkCancellation()
            if let scope {
                return try await service.authorize(
                    accountId: accountId,
                    scope: scope,
                    presentationWindow: presentationWindow
                )
            }
            return try await service.connect(accountId: accountId, presentationWindow: presentationWindow)
        }
        operation = task
        do {
            let catalog = try await task.value
            guard generation == currentGeneration, self.accountId == accountId else { return }
            toolCatalog = try NativeChatMCPToolCatalog.validatedCapacity(catalog)
            lastError = nil
            status = .connected
            operation = nil
        } catch {
            guard generation == currentGeneration, self.accountId == accountId else { return }
            toolCatalog = nil
            if scope != nil, let catalog = try? await service.refreshTools(accountId: accountId) {
                guard generation == currentGeneration, self.accountId == accountId else { return }
                toolCatalog = try? NativeChatMCPToolCatalog.validatedCapacity(catalog)
            }
            guard generation == currentGeneration, self.accountId == accountId else { return }
            lastError = .verbatim(error.localizedDescription)
            status = toolCatalog == nil ? .failed : .connected
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

    func enqueueCleanup(_ accountId: String) {
        if !pendingCleanupAccountIds.contains(accountId) {
            pendingCleanupAccountIds.append(accountId)
        }
    }

    func drainPendingCleanup() async throws {
        while let accountId = pendingCleanupAccountIds.first {
            try await service.disconnect(accountId: accountId)
            pendingCleanupAccountIds.removeAll { $0 == accountId }
        }
    }

}
