import Foundation
import VouchaAPI

extension ViewModel {
    func resetForAccountTransition() {
        activeGenerationTask?.cancel()
        activeGenerationID = UUID()
        accountGeneration = UUID()
        conversationGeneration = UUID()
        apiClient = nil
        currentAccountUserID = nil
        currentPendingTurnOwnerID = nil
        conversations = []
        selectedConversationID = nil
        isLoadingConversations = false
        isLoadingMessages = false
        isLoadingOlderMessages = false
        isCreatingConversation = false
        conversationListCursor = nil
        messagePageCursor = nil
        hasMoreConversations = false
        hasOlderMessages = false
        pendingTurns = [:]
        pendingTurnErrors = [:]
        pendingTurnHistoryAcknowledgements = [:]
        pendingStoreLoadFailed = false
        pendingStoreIsLoading = false
        pendingStoreDiscardFailed = false
        accountExitIsInProgress = false
        accountExitErrorKey = nil
        activeSendOperationID = nil
        messages = []
        draft = ""
        errorMessage = nil
        conversationCreationFailed = false
        conversationCreationErrorMessage = nil
        failedConversationListLoad = nil
        failedConversationHistoryLoad = nil
        latestTranscriptLoadFailure = nil
        loadedConversationHistoryID = nil
        failedGenerations = [:]
        draftBackedGenerationFailures = [:]
        generationErrors = [:]
        isGenerating = false
    }

    func bindAccount(client: APIClient?, userID: String?) async {
        guard currentAccountUserID != userID || apiClient !== client else { return }
        let leavingOwnerID = currentPendingTurnOwnerID
        let leavingDiscard = leavingOwnerID.flatMap { inFlightDiscardsByOwner[$0] }
        let unpreparedTurns = pendingTurns.values.filter { turn in
            let acknowledgements = pendingTurnHistoryAcknowledgements[turn.conversationID, default: []]
            return !acknowledgements.contains(turn.userMessageID) ||
                !acknowledgements.contains(turn.assistantMessageID)
        }
        resetForAccountTransition()
        let transitionRevision = accountGeneration
        await retainUnpreparedTurns(unpreparedTurns, ownerID: leavingOwnerID, discard: leavingDiscard)
        guard accountGeneration == transitionRevision else { return }
        guard let userID, let boundClient = client else { return }
        guard let ownerID = AndroidPendingTurnOwnerNamespace.key(userID: userID, baseURL: boundClient.baseURL) else {
            currentAccountUserID = userID
            apiClient = boundClient
            pendingStoreLoadFailed = true
            return
        }
        await settleInFlightDiscard(ownerID: ownerID)
        guard accountGeneration == transitionRevision else { return }
        currentAccountUserID = userID
        apiClient = boundClient
        currentPendingTurnOwnerID = ownerID

        let accountRevision = accountGeneration
        await loadPendingTurns(ownerID: ownerID)
        guard accountGeneration == accountRevision, currentAccountUserID == userID,
              apiClient === boundClient else { return }

        await loadConversationPage(reset: true)
        if selectedConversationID == nil, let first = conversations.first {
            await selectConversation(first.id)
        }
    }

    private func retainUnpreparedTurns(
        _ turns: [AndroidPendingChatTurn], ownerID: String?, discard: AndroidPendingOwnerDiscardInFlight?
    ) async {
        guard let ownerID, !turns.isEmpty else { return }
        retainLockedPendingTurns(turns, ownerID: ownerID)
        if let discard {
            // A confirmed owner-wide discard owns these turns until its checked result settles.
            if await discard.task.value { forgetLockedDiscardedTurns(discard.turns, ownerID: ownerID) }
            return
        }
        for turn in turns {
            do {
                try await pendingStore.save(ownerID: ownerID, turn: turn)
                forgetLockedPendingTurn(turn, ownerID: ownerID)
            } catch {
                // The turn remains owner-locked in memory until the same account can retry.
            }
        }
    }

    private func settleInFlightDiscard(ownerID: String) async {
        guard let discard = inFlightDiscardsByOwner[ownerID] else { return }
        if await discard.task.value { forgetLockedDiscardedTurns(discard.turns, ownerID: ownerID) }
    }

    func retainLockedPendingTurns(_ turns: [AndroidPendingChatTurn], ownerID: String) {
        for turn in turns where !lockedPendingTurnsByOwner[ownerID, default: []].contains(turn) {
            lockedPendingTurnsByOwner[ownerID, default: []].append(turn)
        }
    }

    func forgetLockedPendingTurn(_ turn: AndroidPendingChatTurn, ownerID: String) {
        lockedPendingTurnsByOwner[ownerID]?.removeAll { $0 == turn }
        if lockedPendingTurnsByOwner[ownerID]?.isEmpty == true {
            lockedPendingTurnsByOwner.removeValue(forKey: ownerID)
        }
    }
}
