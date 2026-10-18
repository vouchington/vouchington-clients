import VouchaCore
import VouchaLocalization

extension ViewModel {
    var chatErrorMessageKey: UiMessageKey? {
        guard errorMessage != nil || transcriptLoadFailed else { return nil }
        if transcriptLoadFailed { return .nativeSwiftEmptyStateUnableToLoad }
        if pendingTurnForSelectedConversation != nil { return .nativeSwiftChatUnableToPersistOnDeviceResponse }
        if conversationCreationFailed { return .nativeSwiftChatUnableToCreateConversation }
        return .nativeSwiftAndroidGenerationFailed
    }

    var transcriptLoadFailure: AndroidTranscriptLoadFailure? {
        latestTranscriptLoadFailure
    }

    var transcriptLoadFailed: Bool {
        failedConversationListLoad != nil || failedConversationHistoryLoad != nil
    }

    var transcriptLoadErrorMessage: String? {
        guard let latestTranscriptLoadFailure else { return nil }
        if failedConversationListLoad?.operation == latestTranscriptLoadFailure {
            return failedConversationListLoad?.message
        }
        if failedConversationHistoryLoad?.operation == latestTranscriptLoadFailure {
            return failedConversationHistoryLoad?.message
        }
        return failedConversationHistoryLoad?.message ?? failedConversationListLoad?.message
    }

    var pendingTurnForSelectedConversation: AndroidPendingChatTurn? {
        guard let selectedConversationID else { return nil }
        return pendingTurns[selectedConversationID]
    }

    var failedGenerationForSelectedConversation: AndroidFailedGeneration? {
        guard let selectedConversationID else { return nil }
        return failedGenerations[selectedConversationID]
    }

    var canSendTranscriptMessage: Bool {
        isSelectedProviderAvailable && !isGenerating && !isCreatingConversation &&
            !isLoadingMessages && !isLoadingConversations && pendingTurnForSelectedConversation == nil &&
            failedGenerationForSelectedConversation == nil &&
            (selectedConversationID == nil || loadedConversationHistoryID == selectedConversationID)
    }
}
