import Foundation
import Observation
import VouchaModels

@MainActor
public protocol NativeChatMCPServicing: AnyObject {
    func connect(accountId: String) async throws -> DecodedJSONValue
    func refreshTools(accountId: String) async throws -> DecodedJSONValue
    func disconnect(accountId: String) async throws
}

/// Keeps an agent's MCP grant and discovered tools scoped to the signed-in member.
@MainActor
@Observable
public final class NativeChatMCPAgentSession {
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

    private let service: any NativeChatMCPServicing
    private var generation = 0
    private var operation: Task<DecodedJSONValue, Error>?

    public init(service: any NativeChatMCPServicing) {
        self.service = service
    }

    public func useAccount(_ nextAccountId: String?) async {
        guard nextAccountId != accountId else { return }
        generation += 1
        let currentGeneration = generation
        operation?.cancel()
        operation = nil
        let previousAccountId = accountId
        accountId = nextAccountId
        toolCatalog = nil
        status = previousAccountId == nil ? .disconnected : .switchingAccount
        if let previousAccountId {
            try? await service.disconnect(accountId: previousAccountId)
        }
        guard generation == currentGeneration else { return }
        status = .disconnected
    }

    public func connect() async throws {
        guard let accountId, status != .switchingAccount, status != .connecting else { return }
        generation += 1
        let currentGeneration = generation
        operation?.cancel()
        status = .connecting
        let task = Task { try await service.connect(accountId: accountId) }
        operation = task
        do {
            let catalog = try await task.value
            guard generation == currentGeneration, self.accountId == accountId else { return }
            toolCatalog = catalog
            status = .connected
            operation = nil
        } catch {
            guard generation == currentGeneration, self.accountId == accountId else { return }
            toolCatalog = nil
            status = .failed
            operation = nil
            throw error
        }
    }

    public func refreshTools() async throws {
        guard let accountId, status == .connected else { return }
        generation += 1
        let currentGeneration = generation
        operation?.cancel()
        let task = Task { try await service.refreshTools(accountId: accountId) }
        operation = task
        do {
            let catalog = try await task.value
            guard generation == currentGeneration, self.accountId == accountId else { return }
            toolCatalog = catalog
            operation = nil
        } catch {
            guard generation == currentGeneration, self.accountId == accountId else { return }
            toolCatalog = nil
            status = .failed
            operation = nil
            throw error
        }
    }

    public func disconnect() async throws {
        guard let accountId else { return }
        generation += 1
        let currentGeneration = generation
        operation?.cancel()
        operation = nil
        toolCatalog = nil
        status = .switchingAccount
        do {
            try await service.disconnect(accountId: accountId)
            guard generation == currentGeneration, self.accountId == accountId else { return }
            status = .disconnected
        } catch {
            guard generation == currentGeneration, self.accountId == accountId else { return }
            status = .failed
            throw error
        }
    }
}
