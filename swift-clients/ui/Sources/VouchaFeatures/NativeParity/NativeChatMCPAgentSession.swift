import Foundation
import Observation
import VouchaAPI
import VouchaLocalization
import VouchaModels

@MainActor
public protocol NativeChatMCPServicing: AnyObject {
    func connect(accountId: String) async throws -> DecodedJSONValue
    func connect(accountId: String, presentationWindow: AnyObject?) async throws -> DecodedJSONValue
    func refreshTools(accountId: String) async throws -> DecodedJSONValue
    func callTool(accountId: String, name: String, arguments: [String: DecodedJSONValue]) async throws
        -> MemberMCPClient.ToolResult
    func disconnect(accountId: String) async throws
}

public extension NativeChatMCPServicing {
    func connect(accountId: String, presentationWindow _: AnyObject?) async throws -> DecodedJSONValue {
        try await connect(accountId: accountId)
    }

    func callTool(
        accountId _: String,
        name _: String,
        arguments _: [String: DecodedJSONValue]
    ) async throws -> MemberMCPClient.ToolResult {
        throw CancellationError()
    }
}

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

    private let service: any NativeChatMCPServicing
    private var generation = 0
    private var operation: Task<DecodedJSONValue, Error>?
    private var pendingCleanupAccountIds: [String] = []
    private var inFlightCalls: [UUID: Task<MemberMCPClient.ToolResult, Error>] = [:]

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

    /// Completes local grant removal while the app still knows which member is signing out.
    public func prepareForSignOut(accountId signingOutAccountId: String) async throws {
        generation += 1
        let currentGeneration = generation
        operation?.cancel()
        operation = nil
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

    public func callTool(
        name: String, arguments: [String: DecodedJSONValue],
        accountId expectedAccountId: String, revision expectedRevision: Int
    ) async throws -> MemberMCPClient.ToolResult {
        guard accountId == expectedAccountId, revision == expectedRevision,
              status == .connected, toolCatalog != nil else { throw CancellationError() }
        let id = UUID()
        let task = Task {
            try await service.callTool(accountId: expectedAccountId, name: name, arguments: arguments)
        }
        inFlightCalls[id] = task
        defer { inFlightCalls.removeValue(forKey: id) }
        let result = try await withTaskCancellationHandler {
            try await task.value
        } onCancel: {
            task.cancel()
        }
        guard accountId == expectedAccountId, revision == expectedRevision,
              status == .connected else { throw CancellationError() }
        return result
    }

    private func cancelCalls() {
        for task in inFlightCalls.values {
            task.cancel()
        }
        inFlightCalls.removeAll()
    }
}
