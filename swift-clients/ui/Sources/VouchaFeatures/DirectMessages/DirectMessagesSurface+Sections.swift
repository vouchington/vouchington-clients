import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

extension DirectMessagesSurface {
    var newMessageView: some View {
        VStack(spacing: Spacing.md) {
            HStack {
                Button(
                    UiMessages.string(.nativeSwiftDirectMessagesInbox, locale: nativeUiLocale),
                    systemImage: "chevron.left"
                ) {
                    returnToInbox()
                }
                Spacer()
            }
            .padding(.horizontal, Spacing.md)

            Form {
                recipientSection
                Section(header: Text(verbatim: UiMessages.string(
                    .nativeSwiftCommunitiesMessage,
                    locale: nativeUiLocale
                ))) {
                    TextEditor(text: messageTextBinding(conversationId: nil))
                        .frame(minHeight: 96)
                    Button(UiMessages.string(.nativeSwiftCommonSend, locale: nativeUiLocale)) {
                        let ids = composeState.selectedRecipients.map(\.id)
                        let recipientQuery = composeState.recipientQuery
                        let text = composeState.messageText(for: nil)
                        Task {
                            let result = await viewModel.createConversation(
                                userIds: ids,
                                text: text
                            )
                            await MainActor.run {
                                switch result {
                                case .created:
                                    isShowingNewMessage = false
                                    isShowingInbox = false
                                    selectedConversationId = viewModel.selectedConversationId
                                    if composeState.selectedRecipients.map(\.id) == ids,
                                       composeState.recipientQuery == recipientQuery {
                                        composeState.selectedRecipients = []
                                        composeState.recipientQuery = ""
                                    }
                                    composeState.clearMessageTextIfUnchanged(text, for: nil)
                                case .ignored:
                                    return
                                case let .failed(createdConversationId):
                                    guard let conversationId = createdConversationId else { return }
                                    if composeState.messageText(for: nil) != text {
                                        return
                                    }
                                    isShowingNewMessage = false
                                    isShowingInbox = false
                                    selectedConversationId = conversationId
                                    composeState.conversationMessageDrafts[conversationId] = text
                                    composeState.newMessageText = ""
                                    composeState.selectedRecipients = []
                                    composeState.recipientQuery = ""
                                }
                            }
                        }
                    }
                    .disabled(composeState.selectedRecipients.isEmpty || composeState.messageText(for: nil)
                        .trimmingCharacters(in: .whitespacesAndNewlines)
                        .isEmpty || viewModel.isCreatingConversation)
                }
            }
        }
    }

    var threadView: some View {
        VStack(spacing: Spacing.md) {
            HStack {
                Button(
                    UiMessages.string(.nativeSwiftDirectMessagesInbox, locale: nativeUiLocale),
                    systemImage: "chevron.left"
                ) {
                    returnToInbox()
                }
                Spacer()
            }
            .padding(.horizontal, Spacing.md)

            messagesList
            participantSection
            HStack {
                TextField(
                    UiMessages.string(.nativeSwiftDirectMessagesWriteAmessage, locale: nativeUiLocale),
                    text: messageTextBinding(conversationId: activeConversationId),
                    axis: .vertical
                )
                .textFieldStyle(.roundedBorder)
                Button(UiMessages.string(.nativeSwiftCommonSend, locale: nativeUiLocale)) {
                    let conversationId = activeConversationId
                    let text = composeState.messageText(for: conversationId)
                    guard !isSendingThreadMessage else { return }
                    isSendingThreadMessage = true
                    Task {
                        defer {
                            Task { @MainActor in
                                isSendingThreadMessage = false
                            }
                        }
                        if await viewModel.sendMessage(text: text) {
                            await MainActor.run {
                                composeState.clearMessageTextIfUnchanged(text, for: conversationId)
                            }
                        }
                    }
                }
                .disabled(composeState.messageText(for: activeConversationId)
                    .trimmingCharacters(in: .whitespacesAndNewlines)
                    .isEmpty || isSendingThreadMessage)
            }
            .padding(.horizontal, Spacing.md)
        }
    }

    var messagesList: some View {
        List {
            if viewModel.hasMoreMessages {
                Button(UiMessages.string(.nativeSwiftDirectMessagesLoadOlderMessages, locale: nativeUiLocale)) {
                    Task { await viewModel.loadMoreMessages() }
                }
            }
            ForEach(viewModel.messages, id: \.id) { message in
                VStack(alignment: .leading, spacing: Spacing.xs) {
                    Text(
                        message.senderUsername
                            ?? UiMessages.string(.nativeSwiftCommonMessage, locale: nativeUiLocale)
                    )
                    .font(Typography.caption)
                    .foregroundStyle(Colors.secondaryLabel)
                    Text(message.bodyText)
                        .font(Typography.body)
                    Text(UiMessages.date(
                        message.createdAt,
                        date: .abbreviated,
                        time: .shortened,
                        locale: nativeUiLocale,
                        timeZone: .current
                    ))
                    .font(Typography.caption)
                    .foregroundStyle(Colors.secondaryLabel)
                }
            }
        }
    }

    var recipientSection: some View {
        Section(header: Text(verbatim: UiMessages.string(
            .nativeSwiftDirectMessagesRecipients,
            locale: nativeUiLocale
        ))) {
            ForEach(composeState.selectedRecipients, id: \.id) { user in
                HStack {
                    Text(verbatim: UiMessages.string(.userContent(directMessageHandle(user)), locale: nativeUiLocale))
                    Spacer()
                    Button(UiMessages.string(.nativeSwiftCommonRemove, locale: nativeUiLocale)) {
                        composeState.selectedRecipients.removeAll { $0.id == user.id }
                    }
                }
            }
            TextField(
                UiMessages.string(.nativeSwiftDirectMessagesSearchUsers, locale: nativeUiLocale),
                text: Binding(
                    get: { composeState.recipientQuery },
                    set: { composeState.recipientQuery = $0 }
                )
            )
            .onSubmit { Task { await viewModel.searchUsers(
                query: composeState.recipientQuery,
                excludeIds: Set(composeState.selectedRecipients.map(\.id))
            ) } }
            Button(UiMessages.string(.nativeSwiftCommonSearch, locale: nativeUiLocale)) {
                Task { await viewModel.searchUsers(
                    query: composeState.recipientQuery,
                    excludeIds: Set(composeState.selectedRecipients.map(\.id))
                ) }
            }
            ForEach(viewModel.userResults.filter { result in
                !composeState.selectedRecipients.contains { $0.id == result.id }
            }, id: \.id) { user in
                Button {
                    composeState.selectedRecipients.append(user)
                    composeState.recipientQuery = ""
                } label: {
                    Text(verbatim: UiMessages.string(.userContent(directMessageHandle(user)), locale: nativeUiLocale))
                }
            }
        }
    }
}
