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
    @State
    var accountReplacement = AndroidAccountReplacementCoordinator()
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
