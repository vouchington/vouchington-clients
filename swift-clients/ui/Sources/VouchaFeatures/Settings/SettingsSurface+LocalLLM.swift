import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

extension SettingsSurface {
    @ViewBuilder
    var localLLMSection: some View {
        if viewModel.localLLMSettingsAvailable {
            section(.nativeSwiftSettingsLocalModels, systemImage: "cpu") {
                Toggle(
                    UiMessages.string(.nativeSwiftSettingsUseLocalModel, locale: nativeUiLocale),
                    isOn: Binding(
                        get: { viewModel.localLLMEnabled },
                        set: { enabled in Task { await viewModel.setLocalLLMEnabled(enabled) } }
                    )
                )
                if let message = viewModel.localLLMStatusMessage {
                    Text(verbatim: UiMessages.string(message, locale: nativeUiLocale))
                        .font(Typography.caption)
                        .foregroundStyle(Colors.secondaryLabel)
                }
                createLocalLLMEndpointForm
                LazyVStack(alignment: .leading, spacing: Spacing.sm) {
                    ForEach(viewModel.localLLMEndpoints) { profile in
                        LocalLLMEndpointEditor(
                            profile: profile,
                            isSelected: viewModel.isLocalLLMEndpointActive(id: profile.id),
                            viewModel: viewModel
                        )
                    }
                }
            }
        }
    }

    private var createLocalLLMEndpointForm: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Group {
                TextField(
                    UiMessages.string(.nativeSwiftSettingsDisplayName, locale: nativeUiLocale),
                    text: $viewModel.localLLMDisplayName
                )
                TextField(
                    UiMessages.string(.nativeSwiftSettingsResponsesApiEndpoint, locale: nativeUiLocale),
                    text: $viewModel.localLLMEndpoint
                )
                SecureField(
                    UiMessages.string(.nativeSwiftSettingsApiKey, locale: nativeUiLocale),
                    text: $viewModel.localLLMAPIKey
                )
            }
            .textFieldStyle(.roundedBorder)
            TextEditor(text: $viewModel.localLLMModelsText)
                .frame(minHeight: 84)
                .overlay(editorBorder)
            TextField(
                UiMessages.string(.nativeSwiftSettingsSelectedModel, locale: nativeUiLocale),
                text: $viewModel.localLLMSelectedModel
            )
            .textFieldStyle(.roundedBorder)
            HStack {
                Button(UiMessages.string(.nativeSwiftSettingsAddLocalModel, locale: nativeUiLocale)) {
                    Task { await viewModel.createLocalLLMEndpoint() }
                }
                .buttonStyle(.borderedProminent)
                Button(UiMessages.string(.nativeSwiftCommonTest, locale: nativeUiLocale)) {
                    Task { await viewModel.testLocalLLMSettings() }
                }
                .buttonStyle(.bordered)
            }
        }
    }
}
