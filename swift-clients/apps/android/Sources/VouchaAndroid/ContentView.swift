import SwiftUI
import VouchaLocalization

enum ContentTab: String, Hashable {
    case chat
    case settings
}

struct ContentView: View {
    @State
    var tab = ContentTab.chat
    @State
    var viewModel = ViewModel()
    @State
    var account = AndroidAccount()
    @State var accountReplacement = AndroidAccountReplacementCoordinator()
    @Environment(\.locale)
    var locale

    var body: some View {
        VStack {
            AndroidAccountRecoveryView(account: account, isReplacing: accountReplacement.isReplacing) {
                Task { await discardAndReplaceUnreadableSession() }
            }
            accountContent
        }
        .onChange(of: account.session.currentUserId) { _, _ in
            Task { await bindAccountSession() }
        }
        .task {
            await account.restore()
            await viewModel.refreshStatus()
            await bindAccountSession()
        }
    }

    private var accountContent: some View {
        Group {
            if account.isRestoring {
                ProgressView()
            } else if account.session.isSignedIn {
                TabView(selection: $tab) {
                    NavigationStack {
                        AndroidChatView(viewModel: viewModel)
                            .navigationTitle(message(.nativeSwiftAndroidChat))
                    }
                    .tabItem { Text(message(.nativeSwiftAndroidChat)) }
                    .tag(ContentTab.chat)

                    NavigationStack {
                        AndroidSettingsView(
                            viewModel: viewModel,
                            signOutErrorMessage: account.session.lastSignOutErrorMessage
                        ) {
                            await signOutAndReplaceAccount()
                        }
                        .navigationTitle(message(.nativeSwiftAndroidLocalModels))
                    }
                    .tabItem { Text(message(.nativeSwiftAndroidSettings)) }
                    .tag(ContentTab.settings)
                }
                .disabled(accountReplacement.isReplacing)
            } else {
                AndroidSignInView(account: account)
            }
        }
    }

    private func message(_ key: UiMessageKey) -> String {
        UiMessages.string(key, locale: locale)
    }

    @MainActor
    private func bindAccountSession() async {
        let userID = account.session.currentUserId
        await viewModel.bindAccount(client: userID == nil ? nil : account.apiClient, userID: userID)
    }

}

struct AndroidChatView: View {
    @Bindable
    var viewModel: ViewModel
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
                            Text(verbatim: UiMessages.string(.userContent(conversation.title), locale: locale))
                        }
                    }
                    if viewModel.hasMoreConversations {
                        Button {
                            Task { await viewModel.loadMoreConversations() }
                        } label: {
                            Image(systemName: "ellipsis")
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
                    .isLoadingConversations)
            }
            .padding(.horizontal)
            if viewModel.hasOlderMessages {
                Button(message(.nativeSwiftDirectMessagesLoadOlderMessages)) {
                    Task { await viewModel.loadOlderMessages() }
                }
                .disabled(viewModel.isLoadingOlderMessages)
            }
            List(viewModel.messages) { chatMessage in
                VStack(alignment: .leading, spacing: 4) {
                    Text(chatMessage.role == "user" ? message(.nativeSwiftChatYou) : message(.nativeSwiftChatAssistant))
                        .font(.caption)
                    Text(chatMessage.content)
                }
            }
            if viewModel.errorMessage != nil {
                Text(message(
                    viewModel.transcriptLoadFailed
                        ? .nativeSwiftEmptyStateUnableToLoad
                        : viewModel.pendingTurnForSelectedConversation != nil
                        ? .nativeSwiftChatUnableToPersistOnDeviceResponse
                        : .nativeSwiftAndroidGenerationFailed
                ))
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
            if viewModel.pendingTurnForSelectedConversation != nil {
                Button(message(.nativeCommonRetry)) {
                    Task { await viewModel.retryPendingTurn() }
                }
                .disabled(viewModel.isGenerating || viewModel.isLoadingMessages)
            }
            HStack {
                TextField(message(.nativeSwiftAndroidMessage), text: $viewModel.draft)
                    .textFieldStyle(.roundedBorder)
                Button(message(.nativeSwiftAndroidSend)) {
                    Task { await viewModel.sendOnDevice() }
                }
                .disabled(!viewModel.isSelectedProviderAvailable || viewModel.isGenerating || viewModel
                    .isLoadingMessages || viewModel.pendingTurnForSelectedConversation != nil)
            }
            .padding()
        }
    }

    private func message(_ key: UiMessageKey) -> String {
        UiMessages.string(key, locale: locale)
    }
}
