import Foundation
import VouchaLocalization

struct AndroidPendingTurnDiscardConfirmation: Equatable {
    let ownerID: String
    let accountGeneration: UUID
}

struct AndroidPendingOwnerDiscardInFlight {
    let token: UUID
    let turns: [AndroidPendingChatTurn]
    let task: Task<Bool, Never>
}

extension ViewModel {
    var canOfferPendingTurnDiscard: Bool {
        (pendingStoreLoadFailed || !pendingTurns.isEmpty) && currentPendingTurnOwnerID != nil && apiClient != nil &&
            !pendingStoreIsLoading && !accountExitIsInProgress && !isGenerating &&
            activeSendOperationID == nil && !isLoadingConversations && !isLoadingMessages &&
            !isLoadingOlderMessages && !isCreatingConversation &&
            currentPendingTurnOwnerID.flatMap { inFlightDiscardsByOwner[$0] } == nil
    }

    /// Capture when showing the destructive confirmation; pass back only after the user confirms.
    func pendingTurnDiscardConfirmation() -> AndroidPendingTurnDiscardConfirmation? {
        guard canOfferPendingTurnDiscard, let ownerID = currentPendingTurnOwnerID else { return nil }
        return AndroidPendingTurnDiscardConfirmation(ownerID: ownerID, accountGeneration: accountGeneration)
    }

    func discardPendingTurns(after confirmation: AndroidPendingTurnDiscardConfirmation) async -> Bool {
        guard canOfferPendingTurnDiscard,
              currentPendingTurnOwnerID == confirmation.ownerID,
              accountGeneration == confirmation.accountGeneration
        else { return false }
        pendingStoreIsLoading = true
        accountExitIsInProgress = true
        pendingStoreDiscardFailed = false
        let pendingStore = pendingStore
        let operation = AndroidPendingOwnerDiscardInFlight(
            token: UUID(), turns: Array(pendingTurns.values), task: Task {
                do {
                    try await pendingStore.discardOwner(ownerID: confirmation.ownerID)
                    return true
                } catch {
                    return false
                }
            }
        )
        inFlightDiscardsByOwner[confirmation.ownerID] = operation
        let succeeded = await operation.task.value
        if inFlightDiscardsByOwner[confirmation.ownerID]?.token == operation.token {
            inFlightDiscardsByOwner.removeValue(forKey: confirmation.ownerID)
        }
        if succeeded {
            forgetLockedDiscardedTurns(operation.turns, ownerID: confirmation.ownerID)
        }
        guard currentPendingTurnOwnerID == confirmation.ownerID,
              accountGeneration == confirmation.accountGeneration else { return false }
        if succeeded {
            let discardedMessageIDs = Set(pendingTurns.values.flatMap { [$0.userMessageID, $0.assistantMessageID] })
            messages.removeAll { discardedMessageIDs.contains($0.id) }
            pendingTurns.removeAll()
            lockedPendingTurnsByOwner.removeValue(forKey: confirmation.ownerID)
            pendingTurnErrors.removeAll()
            pendingTurnHistoryAcknowledgements.removeAll()
            pendingStoreLoadFailed = false
            pendingStoreDiscardFailed = false
            pendingStoreIsLoading = false
            accountExitIsInProgress = false
            restoreSelectedConversationError()
            return true
        }
        pendingStoreDiscardFailed = true
        pendingStoreIsLoading = false
        accountExitIsInProgress = false
        return false
    }

    func forgetLockedDiscardedTurns(_ turns: [AndroidPendingChatTurn], ownerID: String) {
        for turn in turns {
            forgetLockedPendingTurn(turn, ownerID: ownerID)
        }
    }

    func prepareForAccountExit() async -> Bool {
        accountExitErrorKey = nil
        guard !accountExitIsInProgress, !isGenerating, !pendingStoreIsLoading,
              !pendingStoreLoadFailed, !isLoadingConversations, !isLoadingMessages,
              !isLoadingOlderMessages, !isCreatingConversation,
              let ownerID = currentPendingTurnOwnerID
        else {
            accountExitErrorKey = .nativeSwiftChatUnableToPersistOnDeviceResponse
            return false
        }
        let accountRevision = accountGeneration
        accountExitIsInProgress = true
        do {
            for turn in pendingTurns.values {
                try await pendingStore.save(ownerID: ownerID, turn: turn)
                guard accountGeneration == accountRevision, currentPendingTurnOwnerID == ownerID else {
                    return false
                }
            }
            return true
        } catch {
            guard accountGeneration == accountRevision, currentPendingTurnOwnerID == ownerID else { return false }
            accountExitIsInProgress = false
            accountExitErrorKey = .nativeSwiftChatUnableToPersistOnDeviceResponse
            return false
        }
    }

    func cancelAccountExit() {
        accountExitIsInProgress = false
    }

    func retryPendingTurnLoad() async {
        guard pendingStoreLoadFailed, !pendingStoreDiscardFailed,
              !pendingStoreIsLoading, !accountExitIsInProgress,
              let ownerID = currentPendingTurnOwnerID else { return }
        await loadPendingTurns(ownerID: ownerID)
    }

    func loadPendingTurns(ownerID: String) async {
        let accountRevision = accountGeneration
        pendingStoreIsLoading = true
        do {
            let restored = try await pendingStore.load(ownerID: ownerID)
            guard accountGeneration == accountRevision, currentPendingTurnOwnerID == ownerID else { return }
            pendingTurns = try pendingTurnMap(restored: restored, ownerID: ownerID)
            for turn in restored {
                forgetLockedPendingTurn(turn, ownerID: ownerID)
            }
            pendingStoreLoadFailed = false
            pendingStoreIsLoading = false
            pendingStoreDiscardFailed = false
            if let selectedConversationID { appendPendingTurnIfNeeded(for: selectedConversationID) }
            restoreSelectedConversationError()
        } catch {
            guard accountGeneration == accountRevision, currentPendingTurnOwnerID == ownerID else { return }
            pendingStoreLoadFailed = true
            pendingStoreIsLoading = false
        }
    }

    private func pendingTurnMap(
        restored: [AndroidPendingChatTurn], ownerID: String
    ) throws -> [String: AndroidPendingChatTurn] {
        var byConversation: [String: AndroidPendingChatTurn] = [:]
        for turn in restored {
            guard byConversation.updateValue(turn, forKey: turn.conversationID) == nil else {
                throw AndroidPendingTurnStoreError.conflictingTurn
            }
        }
        for turn in lockedPendingTurnsByOwner[ownerID, default: []] {
            if let saved = byConversation[turn.conversationID], saved != turn {
                throw AndroidPendingTurnStoreError.conflictingTurn
            }
            byConversation[turn.conversationID] = turn
        }
        for (conversationID, active) in pendingTurns {
            if let saved = byConversation[conversationID], saved != active {
                throw AndroidPendingTurnStoreError.conflictingTurn
            }
            byConversation[conversationID] = active
        }
        return byConversation
    }
}
