import Foundation
import VouchaAPI
import VouchaCore
import VouchaModels

@MainActor
extension NativeChatMCPAgentSession {
    public func callTool(
        name: String, arguments: [String: DecodedJSONValue],
        accountId expectedAccountId: String, revision expectedRevision: Int,
        beforeAdmission: (@MainActor () throws -> Void)? = nil,
        onStarted: (@MainActor () -> Void)? = nil
    ) async throws -> MemberMCPClient.ToolResult {
        guard accountId == expectedAccountId, revision == expectedRevision,
              status == .connected, toolCatalog != nil else {
            throw LocalLLMToolLoopFailure.toolNotAdmitted
        }
        try Task.checkCancellation()
        try beforeAdmission?()
        guard accountId == expectedAccountId, revision == expectedRevision,
              status == .connected, toolCatalog != nil else {
            throw LocalLLMToolLoopFailure.toolNotAdmitted
        }
        try Task.checkCancellation()
        let id = UUID()
        let task = Task {
            try await service.callTool(accountId: expectedAccountId, name: name, arguments: arguments)
        }
        inFlightCalls[id] = task
        onStarted?()
        defer { inFlightCalls.removeValue(forKey: id) }
        let result: MemberMCPClient.ToolResult
        do {
            result = try await withTaskCancellationHandler {
                try await task.value
            } onCancel: {
                task.cancel()
            }
        } catch MemberMCPTokenManager.Failure.notAuthorized {
            invalidateRejectedAuthorization(accountId: expectedAccountId, revision: expectedRevision)
            throw MemberMCPTokenManager.Failure.notAuthorized
        }
        guard accountId == expectedAccountId, revision == expectedRevision,
              status == .connected else { throw CancellationError() }
        return result
    }

    func cancelCalls() {
        for task in inFlightCalls.values {
            task.cancel()
        }
        inFlightCalls.removeAll()
    }

    func invalidateRejectedAuthorization(accountId expectedAccountId: String, revision expectedRevision: Int) {
        guard accountId == expectedAccountId, revision == expectedRevision, status == .connected else { return }
        revision += 1
        toolCatalog = nil
        cancelCalls()
        status = .failed
        lastError = .verbatim(MemberMCPTokenManager.Failure.notAuthorized.localizedDescription)
    }
}
