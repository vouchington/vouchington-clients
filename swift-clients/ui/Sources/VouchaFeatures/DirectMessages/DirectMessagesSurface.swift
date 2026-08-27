import Observation
import SwiftUI
import VouchaAPI
import VouchaModels

@Observable
final class DirectMessagesComposeState {
    var selectedRecipients: [PublicUser] = []
    var recipientQuery = ""
    var participantQuery = ""
    var newMessageText = ""
    var conversationMessageDrafts: [String: String] = [:]

    func messageText(for conversationId: String?) -> String {
        guard let conversationId else { return newMessageText }
        return conversationMessageDrafts[conversationId, default: ""]
    }

    func setMessageText(_ text: String, for conversationId: String?) {
        guard let conversationId else {
            newMessageText = text
            return
        }
        guard !text.isEmpty else {
            conversationMessageDrafts.removeValue(forKey: conversationId)
            return
        }
        conversationMessageDrafts[conversationId] = text
    }

    func clearMessageTextIfUnchanged(_ text: String, for conversationId: String?) {
        guard messageText(for: conversationId) == text else { return }
        setMessageText("", for: conversationId)
    }
}

struct DirectMessagesSurface: View {
    @Environment(\.locale)
    var nativeUiLocale
    @State
    var viewModel: DirectMessagesViewModel
    @State
    var composeState: DirectMessagesComposeState
    @State
    var selectedConversationId: String?
    @State
    var isShowingInbox = false
    @State
    var isShowingNewMessage = false
    @State
    var isSendingThreadMessage = false
    let initialConversationId: String?

    init(client: APIClient, routeMatch: NativeRouteMatch?, currentUserId: String?) {
        self.init(
            viewModel: DirectMessagesViewModel(client: client, currentUserId: currentUserId),
            routeMatch: routeMatch
        )
    }

    init(
        viewModel: DirectMessagesViewModel,
        routeMatch: NativeRouteMatch?,
        initialSelectedRecipients: [PublicUser] = [],
        initialRecipientQuery: String = "",
        initialMessageText: String = "",
        initialParticipantQuery: String = "",
        initialSelectedConversationId: String? = nil
    ) {
        _viewModel = State(initialValue: viewModel)
        let composeState = DirectMessagesComposeState()
        composeState.selectedRecipients = initialSelectedRecipients
        composeState.recipientQuery = initialRecipientQuery
        composeState.participantQuery = initialParticipantQuery
        let initialDraftConversationId = initialSelectedConversationId
            ?? routeMatch?.param("conversationId").flatMap { $0 == "new" ? nil : $0 }
        if let initialDraftConversationId {
            composeState.conversationMessageDrafts[initialDraftConversationId] = initialMessageText
        } else {
            composeState.newMessageText = initialMessageText
        }
        _composeState = State(initialValue: composeState)
        _selectedConversationId = State(initialValue: initialSelectedConversationId)
        _isShowingNewMessage = State(initialValue: routeMatch?.path == "/messages/new")
        initialConversationId = routeMatch?.param("conversationId")
    }

    var body: some View {
        Group {
            if activeConversationId != nil {
                threadView
            } else if isShowingNewMessage {
                newMessageView
            } else if isShowingInbox {
                inboxView
            } else {
                inboxView
            }
        }
        .task(id: loadTaskId) {
            if selectedConversationId != nil {
                return
            }
            if let activeConversationId {
                await viewModel.loadThread(conversationId: activeConversationId)
            } else if isShowingInbox || !isShowingNewMessage {
                await viewModel.loadInbox()
            }
        }
    }

    func messageTextBinding(conversationId: String?) -> Binding<String> {
        Binding(
            get: { composeState.messageText(for: conversationId) },
            set: { composeState.setMessageText($0, for: conversationId) }
        )
    }

    func openNewMessageComposer() {
        isShowingInbox = false
        isShowingNewMessage = true
    }

    func openConversation(_ conversationId: String) async {
        if activeConversationId != conversationId {
            composeState.participantQuery = ""
        }
        isShowingInbox = false
        isShowingNewMessage = false
        selectedConversationId = conversationId
        await viewModel.loadThread(conversationId: conversationId)
    }

    func returnToInbox() {
        isShowingInbox = true
        isShowingNewMessage = false
        selectedConversationId = nil
        composeState.participantQuery = ""
        viewModel.clearSelectedConversation()
    }

}
