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
    @Environment(\.locale)
    var locale

    var body: some View {
        TabView(selection: $tab) {
            NavigationStack {
                AndroidChatView(viewModel: viewModel)
                    .navigationTitle(message(.nativeSwiftAndroidChat))
            }
            .tabItem { Text(message(.nativeSwiftAndroidChat)) }
            .tag(ContentTab.chat)

            NavigationStack {
                AndroidSettingsView(viewModel: viewModel)
                    .navigationTitle(message(.nativeSwiftAndroidLocalModels))
            }
            .tabItem { Text(message(.nativeSwiftAndroidSettings)) }
            .tag(ContentTab.settings)
        }
        .task { await viewModel.refreshStatus() }
    }

    private func message(_ key: UiMessageKey) -> String {
        UiMessages.string(key, locale: locale)
    }
}

struct AndroidChatView: View {
    @Bindable
    var viewModel: ViewModel
    @Environment(\.locale)
    var locale

    var body: some View {
        VStack {
            List(viewModel.messages) { chatMessage in
                VStack(alignment: .leading, spacing: 4) {
                    Text(chatMessage.role == "user" ? message(.nativeSwiftChatYou) : message(.nativeSwiftChatAssistant))
                        .font(.caption)
                    Text(chatMessage.content)
                }
            }
            if viewModel.errorMessage != nil {
                Text(message(.nativeSwiftAndroidGenerationFailed))
                    .foregroundStyle(.red)
                    .padding(.horizontal)
            }
            HStack {
                TextField(message(.nativeSwiftAndroidMessage), text: $viewModel.draft)
                    .textFieldStyle(.roundedBorder)
                Button(message(.nativeSwiftAndroidSend)) {
                    Task { await viewModel.sendOnDevice() }
                }
                .disabled(!viewModel.isSelectedProviderAvailable || viewModel.isGenerating)
            }
            .padding()
        }
    }

    private func message(_ key: UiMessageKey) -> String {
        UiMessages.string(key, locale: locale)
    }
}

struct AndroidSettingsView: View {
    @Bindable
    var viewModel: ViewModel
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
