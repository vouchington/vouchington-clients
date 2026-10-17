import Foundation
import VouchaLocalization

extension NativeChatViewModel {
    func prepareMCPReceiptTurn(
        context: NativeChatDraftSendContext,
        snapshot: NativeChatMCPAgentSession.ConnectedSnapshot?
    ) -> NativeChatMCPTurnContext? {
        guard case .appleFoundationModels = context.providerSelection, let snapshot,
              isCurrentMCPSnapshot(snapshot), isCurrentLocalGeneration(context) else { return nil }
        let owner = NativeChatMCPTurnOwner(
            accountId: snapshot.accountId, revision: snapshot.revision,
            conversationId: context.conversationId, userMessageId: context.userMessageId
        )
        if mcpReceiptLedger?.owner != owner {
            expireMCPReceipts()
            mcpReceiptLedger = .init(owner: owner)
        }
        let isCurrent: @MainActor @Sendable () -> Bool = { [weak self] in
            guard let self else { return false }
            return !Task.isCancelled && isCurrentLocalGeneration(context)
                && isCurrentMCPSnapshot(snapshot) && mcpReceiptLedger?.owner == owner
        }
        return .init(owner: owner, isCurrent: isCurrent, publish: { [weak self] receipt in
            guard isCurrent() else { return false }
            return self?.mcpReceiptLedger?.record(receipt) == true
        })
    }

    func expireMCPReceipts() {
        if let id = mcpReceiptLedger?.temporaryUserMessageId { discardConversationMessage(id: id) }
        mcpReceiptLedger = nil
    }

    func invalidateMCPReceiptsIfNeeded() {
        guard let owner = mcpReceiptLedger?.owner else { return }
        guard currentMemberId == owner.accountId,
              mcpSession?.accountId == owner.accountId,
              mcpSession?.revision == owner.revision else {
            expireMCPReceipts()
            return
        }
    }

    func retainTemporaryMCPUserTurn(_ id: String) -> Bool {
        guard let ledger = mcpReceiptLedger, ledger.owner.userMessageId == id,
              !ledger.receipts.isEmpty, currentMemberId == ledger.owner.accountId,
              mcpSession?.accountId == ledger.owner.accountId,
              mcpSession?.revision == ledger.owner.revision else { return false }
        mcpReceiptLedger?.temporaryUserMessageId = id
        return true
    }

    func finishLocalToolResults(
        context: NativeChatDraftSendContext, assistantMessageId: String,
        continuation: NativeChatMCPContinuation
    ) {
        guard isCurrentLocalGeneration(context) else { return }
        guard retainTemporaryMCPUserTurn(context.userMessageId) else {
            failLocalGeneration(
                context: context, assistantMessageId: assistantMessageId,
                message: .message(.sharedChatMcpPossibleSideEffect), restoreDraft: false
            )
            return
        }
        mcpReceiptLedger?.continuation = continuation
        discardConversationMessage(id: assistantMessageId)
        pendingLocalTurn = nil
        if !draftEditedDuringSend { draftMessage = "" }
        isStreaming = false
        streamTask = nil
        streamingConversationId = nil
        streamingUserMessageId = nil
        streamingAssistantMessageId = nil
        pendingLocalAssistantMessageId = nil
        streamedContent = ""
    }
}
