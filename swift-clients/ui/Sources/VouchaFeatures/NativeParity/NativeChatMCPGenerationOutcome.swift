import Foundation

struct NativeChatMCPTurnOwner: Equatable {
    let accountId: String
    let revision: Int
    let conversationId: String
    let userMessageId: String
}

struct NativeChatMCPReceipt: Identifiable, Equatable {
    enum Kind: Equatable { case response, toolError }

    let id: UUID
    /// Model-provider call identity; distinct from the local UI row identity.
    let callId: String
    let owner: NativeChatMCPTurnOwner
    let toolName: String
    /// The original bridge-validated model envelope, never presentation copy.
    let envelope: String
    let kind: Kind
    let fact: NativeChatMCPFact?
}

struct NativeChatMCPTurnContext {
    let owner: NativeChatMCPTurnOwner
    let isCurrent: @MainActor @Sendable () -> Bool
    /// Publication is nonthrowing and guarded by the mounted account and turn.
    let publish: @MainActor @Sendable (NativeChatMCPReceipt) -> Bool
}

enum NativeChatMCPContinuation: Equatable {
    case running
    case contextStopped
    case processingStopped
    case cancelled
    case ambiguous
}

enum NativeChatMCPGenerationOutcome {
    case answer(NativeChatAssistantResponse)
    case toolResultsOnly(NativeChatMCPContinuation)
}

struct NativeChatMCPReceiptLedger {
    let owner: NativeChatMCPTurnOwner
    private(set) var receipts: [NativeChatMCPReceipt] = []
    var continuation: NativeChatMCPContinuation = .running
    var displayUserMessageId: String
    var temporaryUserMessageId: String?

    init(owner: NativeChatMCPTurnOwner) {
        self.owner = owner
        displayUserMessageId = owner.userMessageId
    }

    mutating func record(_ receipt: NativeChatMCPReceipt) -> Bool {
        guard receipt.owner.accountId == owner.accountId, receipt.owner.revision == owner.revision,
              receipt.owner.conversationId == owner.conversationId, receipt.owner.userMessageId == owner.userMessageId,
              !receipt.toolName.isEmpty, !receipt.callId.isEmpty, receipts.count < 4,
              !receipts.contains(where: { $0.id == receipt.id }) else { return false }
        receipts.append(receipt)
        return true
    }
}
