import Foundation
import VouchaAPI
import VouchaModels

@MainActor
extension NativeChatMCPAgentSession {
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

    func cancelCalls() {
        for task in inFlightCalls.values {
            task.cancel()
        }
        inFlightCalls.removeAll()
    }
}
