import VouchaCore
import VouchaLocalization

enum AndroidChatErrorKind: Equatable {
    case other
    case model
    case generation
}

extension ViewModel {
    var generationErrorForSelectedConversation: String? {
        guard let id = selectedConversationID else { return nil }
        return generationErrors[id] ?? failedGenerations[id]?.message
    }

    var chatErrorMessageKey: UiMessageKey? {
        guard errorMessage != nil || transcriptLoadFailed || pendingStoreLoadFailed else { return nil }
        if pendingStoreLoadFailed { return .nativeSwiftAndroidSavedResponsesUnreadable }
        if pendingTurnForSelectedConversation != nil { return .nativeSwiftChatUnableToPersistOnDeviceResponse }
        if chatErrorKind == .generation { return .nativeSwiftAndroidGenerationFailed }
        if conversationCreationFailed { return .nativeSwiftChatUnableToCreateConversation }
        if transcriptLoadFailed { return .nativeSwiftEmptyStateUnableToLoad }
        if chatErrorKind == .model { return .nativeSwiftAndroidModelUnavailable }
        return .nativeSwiftEmptyStateUnableToLoad
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

    var replaceableTranscriptMessageIDs: Set<String> {
        let pending = pendingTurnForSelectedConversation
        let protectedIDs = [pending?.userMessageID, pending?.assistantMessageID].compactMap { $0 }
        return Set(messages.map(\.id)).subtracting(protectedIDs)
    }

    var unlistedPendingTurns: [AndroidPendingChatTurn] {
        let listedIDs = Set(conversations.map(\.id))
        return pendingTurns.values.filter { !listedIDs.contains($0.conversationID) }
            .sorted { $0.conversationID < $1.conversationID }
    }

    var failedGenerationForSelectedConversation: AndroidFailedGeneration? {
        guard let selectedConversationID else { return nil }
        return failedGenerations[selectedConversationID]
    }

    var canSendTranscriptMessage: Bool {
        !pendingStoreLoadFailed && !pendingStoreIsLoading && !accountExitIsInProgress &&
            isSelectedProviderAvailable &&
            !isGenerating && !isCreatingConversation &&
            !isLoadingMessages && !isLoadingConversations && pendingTurnForSelectedConversation == nil &&
            failedGenerationForSelectedConversation == nil &&
            (selectedConversationID == nil || loadedConversationHistoryID == selectedConversationID)
    }
}
