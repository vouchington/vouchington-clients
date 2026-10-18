import Foundation

extension ViewModel {
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
