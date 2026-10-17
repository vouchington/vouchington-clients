import Foundation
import VouchaLocalization

extension NativeChatViewModel {
    func failLocalGeneration(
        context: NativeChatDraftSendContext,
        assistantMessageId: String,
        message: UiVerbatimText,
        restoreDraft: Bool = true
    ) {
        guard isCurrentLocalGeneration(context) else {
            discardStaleLocalTurn(
                userMessageId: context.userMessageId,
                assistantMessageId: assistantMessageId
            )
            return
        }
        failUnpersistedLocalGeneration(
            context: context,
            assistantMessageId: assistantMessageId,
            message: message,
            restoreDraft: restoreDraft
        )
    }

    func failUnpersistedLocalGeneration(
        context: NativeChatDraftSendContext,
        assistantMessageId: String?,
        message: UiVerbatimText,
        restoreDraft: Bool = true
    ) {
        invalidateMCPReceiptsIfNeeded()
        guard isCurrentLocalGeneration(context) else {
            discardConversationMessage(id: context.userMessageId)
            if let assistantMessageId { discardConversationMessage(id: assistantMessageId) }
            return
        }
        if !retainTemporaryMCPUserTurn(context.userMessageId) {
            discardConversationMessage(id: context.userMessageId)
        } else if !restoreDraft {
            mcpReceiptLedger?.continuation = .ambiguous
        }
        if let assistantMessageId { discardConversationMessage(id: assistantMessageId) }
        if restoreDraft, !draftEditedDuringSend { draftMessage = context.text }
        if streamingUserMessageId == context.userMessageId {
            streamingUserMessageId = nil
        }
        if streamingAssistantMessageId == assistantMessageId {
            streamingAssistantMessageId = nil
        }
        if pendingLocalAssistantMessageId == assistantMessageId {
            pendingLocalAssistantMessageId = nil
        }
        streamErrorMessage = message
        isStreaming = false
        streamingConversationId = nil
        streamedContent = ""
        streamTask = nil
    }
}
