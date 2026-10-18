import SwiftUI
import VouchaLocalization

struct AndroidSettingsView: View {
    @Bindable
    var viewModel: ViewModel
    var signOutErrorMessage: String?
    var signOut: () async -> Void
    @State
    var endpointStatus: String?
    @Environment(\.locale)
    var locale

    var body: some View {
        Form {
            Section(message(.nativeSwiftAndroidProviderAicore)) {
                Text(modelStatusText)
                if viewModel.modelState == .downloadable {
                    Button(message(.nativeSwiftAndroidDownloadModel)) {
                        Task { await viewModel.downloadModel() }
                    }
                }
                Button(message(.nativeSwiftAndroidCheckModelStatus)) {
                    Task { await viewModel.refreshStatus() }
                }
            }
            Section(message(.nativeSwiftAndroidOpenAiCompatibleEndpoint)) {
                Picker(message(.nativeSwiftAndroidChatProvider), selection: Binding(
                    get: { viewModel.selectedProvider },
                    set: { provider in Task { await viewModel.selectProvider(provider) } }
                )) {
                    Text(message(.nativeSwiftAndroidProviderAicore)).tag(AndroidChatProvider.aiCore)
                    Text(message(.nativeSwiftAndroidConfiguredEndpoint)).tag(AndroidChatProvider.endpoint)
                }
                TextField(message(.nativeSwiftAndroidEndpointUrl), text: $viewModel.endpoint)
                TextField(message(.nativeSwiftAndroidModelName), text: $viewModel.endpointModel)
                SecureField(message(.nativeSwiftAndroidApiKey), text: $viewModel.endpointAPIKey)
                Button(message(.nativeSwiftAndroidSaveEndpoint)) {
                    Task {
                        do {
                            try await viewModel.saveEndpointSettings()
                            endpointStatus = message(.nativeSwiftAndroidEndpointSaved)
                        } catch {
                            endpointStatus = message(.nativeSwiftAndroidEndpointSaveFailed)
                        }
                    }
                }
                if let endpointStatus {
                    Text(endpointStatus)
                }
            }
            Section(message(.nativeSwiftProfileSignOut)) {
                Button(message(.nativeSwiftProfileSignOut), role: .destructive) {
                    Task { await signOut() }
                }
                if let signOutErrorMessage {
                    Text(verbatim: UiMessages.string(UiVerbatimText.verbatim(signOutErrorMessage), locale: locale))
                        .foregroundStyle(.red)
                }
                if let accountExitErrorKey = viewModel.accountExitErrorKey {
                    Text(message(accountExitErrorKey)).foregroundStyle(.red)
                }
            }
        }
    }

    var modelStatusText: String {
        switch viewModel.modelState {
        case .checking: message(.nativeSwiftAndroidStatusChecking)
        case .unavailable: message(.nativeSwiftAndroidModelUnavailable)
        case .downloadable: message(.nativeSwiftAndroidModelDownloadable)
        case .downloading: message(.nativeSwiftAndroidDownloadingModel)
        case .available: message(.nativeSwiftAndroidModelAvailable)
        }
    }

    private func message(_ key: UiMessageKey) -> String {
        UiMessages.string(key, locale: locale)
    }
}
