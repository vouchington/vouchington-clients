import Foundation

extension ViewModel {
    func loadMoreConversations() async {
        guard failedConversationListLoad == nil else { return }
        await loadConversationPage(reset: false)
    }

    func loadOlderMessages() async {
        guard failedConversationHistoryLoad == nil else { return }
        await loadConversationMessages(reset: false)
    }
}
