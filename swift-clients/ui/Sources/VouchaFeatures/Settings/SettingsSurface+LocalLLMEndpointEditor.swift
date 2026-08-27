import SwiftUI
import VouchaCore
import VouchaDesignSystem
import VouchaLocalization

struct LocalLLMEndpointEditor: View {
    @Environment(\.locale)
    var nativeUiLocale
    let profile: LocalLLMEndpointProfile
    let isSelected: Bool
    @Bindable
    var viewModel: SettingsViewModel
    @State
    private var interactionState: LocalLLMEndpointEditorInteractionState

    init(
        profile: LocalLLMEndpointProfile,
        isSelected: Bool,
        viewModel: SettingsViewModel,
        interactionState: LocalLLMEndpointEditorInteractionState? = nil
    ) {
        self.profile = profile
        self.isSelected = isSelected
        self.viewModel = viewModel
        _interactionState = State(
            initialValue: interactionState ?? LocalLLMEndpointEditorInteractionState(profile: profile)
        )
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            header
            Group {
                TextField(
                    UiMessages.string(.nativeSwiftSettingsDisplayName, locale: nativeUiLocale),
                    text: $interactionState.draft.displayName
                )
                TextField(
                    UiMessages.string(.nativeSwiftSettingsResponsesApiEndpoint, locale: nativeUiLocale),
                    text: $interactionState.draft.endpoint
                )
                SecureField(
                    UiMessages.string(.nativeSwiftSettingsApiKey, locale: nativeUiLocale),
                    text: $interactionState.draft.apiKey
                )
                .onChange(of: interactionState.draft.apiKey) { _, newValue in
                    interactionState.noteAPIKeyChanged(to: newValue)
                }
            }
            .textFieldStyle(.roundedBorder)
            Toggle(
                UiMessages.string(.nativeSwiftSettingsUseLocalModel, locale: nativeUiLocale),
                isOn: $interactionState.draft.isEnabled
            )
            TextEditor(text: $interactionState.draft.modelsText)
                .frame(minHeight: 60)
                .overlay(RoundedRectangle(cornerRadius: 6, style: .continuous).strokeBorder(.quaternary, lineWidth: 1))
            TextField(
                UiMessages.string(.nativeSwiftSettingsSelectedModel, locale: nativeUiLocale),
                text: $interactionState.draft.selectedModel
            )
            .textFieldStyle(.roundedBorder)
            HStack {
                Button(UiMessages.string(.nativeSwiftCommonSave, locale: nativeUiLocale)) {
                    Task {
                        let submittedDraft = interactionState.draft
                        interactionState.markSubmitted(submittedDraft)
                        let submittedAPIKeyWasEdited = interactionState.apiKeyWasEdited
                        let didSave = await viewModel.updateLocalLLMEndpoint(
                            id: profile.id,
                            draft: submittedDraft,
                            apiKeyWasEdited: submittedAPIKeyWasEdited
                        )
                        if didSave {
                            interactionState.applySuccessfulSave(submittedDraft: submittedDraft)
                        }
                    }
                }
                .buttonStyle(.bordered)
                Button(UiMessages.string(.nativeSwiftCommonTest, locale: nativeUiLocale)) {
                    Task { await viewModel.testLocalLLMEndpoint(draft: interactionState.draft) }
                }
                .buttonStyle(.bordered)
            }
        }
        .padding(Spacing.sm)
        .overlay(
            RoundedRectangle(cornerRadius: 8, style: .continuous)
                .strokeBorder(.quaternary, lineWidth: 1)
        )
        .onChange(of: profile) { oldValue, newValue in
            interactionState.applyProfileChange(from: oldValue, to: newValue)
        }
        .task(id: profile.credentialLoadIdentity) {
            let key = await viewModel.loadLocalLLMEndpointAPIKey(id: profile.id)
            interactionState.applyLoadedAPIKey(key, isCancelled: Task.isCancelled)
        }
    }

    private var header: some View {
        HStack {
            VStack(alignment: .leading, spacing: 2) {
                Text(
                    profile.displayName.isEmpty
                        ? UiMessages.string(.nativeSwiftSettingsUnnamedLocalModel, locale: nativeUiLocale)
                        : profile.displayName
                )
                .font(Typography.subheadline)
                .fontWeight(.semibold)
                if isSelected {
                    Text(UiMessages.string(.nativeSwiftCommonCurrent, locale: nativeUiLocale))
                        .font(Typography.caption)
                        .foregroundStyle(Colors.secondaryLabel)
                }
            }
            Spacer(minLength: 0)
            if !isSelected, profile.isEnabled {
                Button(UiMessages.string(.nativeSwiftCommonActivate, locale: nativeUiLocale)) {
                    Task { await viewModel.selectLocalLLMEndpoint(id: profile.id) }
                }
                .buttonStyle(.bordered)
            }
            Button(UiMessages.string(.nativeSwiftCommonDelete, locale: nativeUiLocale), role: .destructive) {
                Task { await viewModel.deleteLocalLLMEndpoint(id: profile.id) }
            }
            .buttonStyle(.bordered)
        }
    }

}
