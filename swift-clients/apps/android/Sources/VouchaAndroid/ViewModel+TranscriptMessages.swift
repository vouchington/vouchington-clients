import Foundation
import VouchaModels

extension ViewModel {
    func acknowledgePendingTurnIfPresent(
        in serverMessages: [ChatMessage],
        conversationID: String,
        reset: Bool
    ) -> Set<String> {
        guard let pending = pendingTurns[conversationID] else { return [] }
        var acknowledgements = reset ? [] : pendingTurnHistoryAcknowledgements[conversationID, default: []]
        var currentPageAcknowledgements = Set<String>()
        for message in serverMessages where message.conversationId == conversationID {
            if message.id == pending.userMessageID, message.content.role == "user" {
                acknowledgements.insert(pending.userMessageID)
                currentPageAcknowledgements.insert(pending.userMessageID)
            } else if message.id == pending.assistantMessageID, message.content.role == "assistant" {
                acknowledgements.insert(pending.assistantMessageID)
                currentPageAcknowledgements.insert(pending.assistantMessageID)
            }
        }
        guard acknowledgements.contains(pending.userMessageID),
              acknowledgements.contains(pending.assistantMessageID)
        else {
            pendingTurnHistoryAcknowledgements[conversationID] = acknowledgements
            return currentPageAcknowledgements
        }
        pendingTurns.removeValue(forKey: conversationID)
        pendingTurnErrors.removeValue(forKey: conversationID)
        pendingTurnHistoryAcknowledgements.removeValue(forKey: conversationID)
        return currentPageAcknowledgements
    }

    func sortMessagesChronologically() {
        messages.sort {
            if $0.createdAt == $1.createdAt { return $0.id < $1.id }
            return $0.createdAt < $1.createdAt
        }
    }

    func appendPendingTurnIfNeeded(for conversationID: String) {
        guard let pending = pendingTurns[conversationID] else { return }
        if !messages.contains(where: { $0.id == pending.userMessageID }) {
            messages.append(.init(id: pending.userMessageID, role: "user", content: pending.userText))
        }
        if !messages.contains(where: { $0.id == pending.assistantMessageID }) {
            messages.append(.init(id: pending.assistantMessageID, role: "assistant", content: pending.assistantText))
        }
        sortMessagesChronologically()
    }
}
