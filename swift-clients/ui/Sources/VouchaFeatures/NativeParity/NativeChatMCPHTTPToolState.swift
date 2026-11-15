import Foundation

/// Provider-neutral call ledger for the HTTP (OpenAI-compatible) tool loop. A call counts as
/// completed only after its validated result was published under the trusted turn owner.
@MainActor
final class NativeChatMCPHTTPToolState {
    let turn: NativeChatMCPTurnContext
    private(set) var started = 0
    private(set) var receipts: [NativeChatMCPReceipt] = []

    init(turn: NativeChatMCPTurnContext) {
        self.turn = turn
    }

    var attempted: Bool {
        started > 0
    }

    /// A started call without a published result may or may not have changed server state.
    var hasAmbiguousAttempt: Bool {
        started > receipts.count
    }

    /// Every started call finished and was published, so later failure cannot hide a completed action.
    var completedAllCalls: Bool {
        !receipts.isEmpty && !hasAmbiguousAttempt
    }

    func markStarted() {
        started += 1
    }

    func record(callId: String, name: String, result: NativeChatMCPToolBridge.ValidatedResult) throws {
        let receipt = NativeChatMCPReceipt(
            id: UUID(), callId: callId, owner: turn.owner, toolName: name, envelope: result.envelope,
            kind: result.isError ? .toolError : .response, fact: result.fact
        )
        guard !Task.isCancelled, turn.publish(receipt) else { throw CancellationError() }
        receipts.append(receipt)
    }
}
