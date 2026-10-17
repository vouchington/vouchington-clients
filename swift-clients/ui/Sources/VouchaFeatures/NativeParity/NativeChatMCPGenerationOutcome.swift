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
    let owner: NativeChatMCPTurnOwner
    let toolName: String
    /// The original bridge-validated model envelope, never presentation copy.
    let envelope: String
    let kind: Kind
}

struct NativeChatMCPTurnContext {
    let owner: NativeChatMCPTurnOwner
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
        guard receipt.owner == owner, receipts.count < 4,
              !receipts.contains(where: { $0.id == receipt.id }) else { return false }
        receipts.append(receipt)
        return true
    }
}
