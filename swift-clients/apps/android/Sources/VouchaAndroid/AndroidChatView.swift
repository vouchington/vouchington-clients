import SwiftUI
import VouchaLocalization

struct AndroidChatView: View {
    @Bindable
    var viewModel: ViewModel
    @State
    var discardConfirmation: AndroidPendingTurnDiscardConfirmation?
    @Environment(\.locale)
    var locale

    var body: some View {
        VStack {
            HStack {
                Menu {
                    ForEach(viewModel.conversations) { conversation in
                        Button {
                            Task { await viewModel.selectConversation(conversation.id) }
                        } label: {
                            Text(verbatim: androidConversationMenuTitle(conversation.title, locale: locale))
                        }
                    }
                    ForEach(viewModel.unlistedPendingTurns, id: \.conversationID) { pending in
                        Button {
                            Task { await viewModel.selectConversation(pending.conversationID) }
                        } label: {
                            Label {
                                Text(verbatim: UiMessages.string(
                                    .userContent(String(pending.userText.prefix(60))), locale: locale
                                ))
                            } icon: {
                                Image(systemName: "clock.arrow.circlepath")
                            }
                        }
                    }
                    if viewModel.hasMoreConversations {
                        Button {
                            Task { await viewModel.loadMoreConversations() }
                        } label: {
                            Label(message(.nativeSwiftCommonLoadMore), systemImage: "ellipsis")
                        }
                    }
                } label: {
                    Text(message(.nativeSwiftChatChats))
                }
                .disabled(viewModel.isLoadingConversations)

                Button(message(.nativeSwiftChatNewConversation)) {
                    Task { await viewModel.createConversation() }
                }
                .disabled(viewModel.isCreatingConversation || viewModel.isGenerating || viewModel
                    .isLoadingConversations || viewModel.pendingStoreIsLoading || viewModel.pendingStoreLoadFailed)
            }
            .padding(.horizontal)
            if viewModel.hasOlderMessages {
                Button(message(.nativeSwiftDirectMessagesLoadOlderMessages)) {
                    Task { await viewModel.loadOlderMessages() }
                }
                .disabled(viewModel.isLoadingOlderMessages || viewModel.isGenerating)
            }
            List(viewModel.messages) { chatMessage in
                VStack(alignment: .leading, spacing: 4) {
                    Text(chatMessage.role == "user" ? message(.nativeSwiftChatYou) : message(.nativeSwiftChatAssistant))
                        .font(.caption)
                    if !chatMessage.content.isEmpty {
                        Text(chatMessage.content)
                    }
                    if let error = chatMessage.error {
                        Text(verbatim: UiMessages.string(error, locale: locale))
                            .foregroundStyle(.red)
                    }
                }
            }
            if let errorKey = viewModel.chatErrorMessageKey {
                Text(message(errorKey))
                    .foregroundStyle(.red)
                    .padding(.horizontal)
            }
            if viewModel.transcriptLoadFailed,
               viewModel.apiClient != nil,
               viewModel.pendingTurnForSelectedConversation == nil {
                Button(message(.nativeCommonRetry)) {
                    Task { await viewModel.retryConversationLoad() }
                }
                .disabled(viewModel.isLoadingConversations || viewModel.isLoadingMessages)
            }
            if viewModel.pendingStoreLoadFailed {
                if !viewModel.pendingStoreDiscardFailed {
                    Button(message(.nativeCommonRetry)) {
                        Task { await viewModel.retryPendingTurnLoad() }
                    }
                    .disabled(viewModel.pendingStoreIsLoading)
                }
            }
            if viewModel.pendingStoreLoadFailed || !viewModel.pendingTurns.isEmpty {
                Button(message(.nativeSwiftAndroidDeleteSavedResponses), role: .destructive) {
                    discardConfirmation = viewModel.pendingTurnDiscardConfirmation()
                }
                .disabled(!viewModel.canOfferPendingTurnDiscard)
            }
            if viewModel.pendingTurnForSelectedConversation != nil {
                Button(message(.nativeCommonRetry)) {
                    Task { await viewModel.retryPendingTurn() }
                }
                .disabled(viewModel.isGenerating || viewModel.isLoadingMessages || viewModel.pendingStoreIsLoading ||
                    viewModel.pendingStoreDiscardFailed)
            }
            if viewModel.failedGenerationForSelectedConversation != nil {
                Button(message(.nativeCommonRetry)) {
                    Task { await viewModel.retryFailedGeneration() }
                }
                .disabled(viewModel.isGenerating || viewModel.isLoadingMessages || viewModel.isLoadingConversations)
            }
            HStack {
                TextField(message(.nativeSwiftAndroidMessage), text: $viewModel.draft)
                    .textFieldStyle(.roundedBorder)
                Button(message(.nativeSwiftAndroidSend)) {
                    Task { await viewModel.sendOnDevice() }
                }
                .disabled(!viewModel.canSendTranscriptMessage)
            }
            .padding()
        }
        .onChange(of: viewModel.accountGeneration) { _, _ in
            discardConfirmation = nil
        }
        .alert(
            message(.nativeSwiftAndroidDeleteSavedResponses),
            isPresented: Binding(
                get: { discardConfirmation != nil },
                set: { if !$0 { discardConfirmation = nil } }
            )
        ) {
            if let confirmation = discardConfirmation {
                Button(message(.nativeSwiftAndroidDeleteSavedResponses), role: .destructive) {
                    discardConfirmation = nil
                    Task { await viewModel.discardPendingTurns(after: confirmation) }
                }
            }
            Button(message(.commonCancel), role: .cancel) {
                discardConfirmation = nil
            }
        } message: {
            Text(message(.nativeSwiftAndroidDeleteSavedResponsesConfirmation))
        }
    }

    private func message(_ key: UiMessageKey) -> String {
        UiMessages.string(key, locale: locale)
    }
}
