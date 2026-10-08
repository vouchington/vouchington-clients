import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension SettingsSurface {
    var profileSection: some View {
        section(.nativeSwiftSettingsProfile, systemImage: "square.and.pencil") {
            TextEditor(text: $viewModel.profileMarkdown)
                .frame(minHeight: 120)
                .overlay(editorBorder)

            HStack {
                Button(UiMessages.string(.nativeSwiftSettingsSaveBio, locale: nativeUiLocale)) {
                    Task { await viewModel.saveProfile() }
                }
                .buttonStyle(.borderedProminent)
                .disabled(viewModel.isLoading)
                Spacer()
            }

            VStack(alignment: .leading, spacing: Spacing.sm) {
                Text(UiMessages.string(.nativeSwiftSettingsProfileLinks, locale: nativeUiLocale))
                    .font(Typography.subheadline)
                    .fontWeight(.semibold)
                createProfileLinkForm
                LazyVStack(alignment: .leading, spacing: Spacing.sm) {
                    ForEach(viewModel.profileLinks) { link in
                        ProfileLinkEditor(link: link, viewModel: viewModel)
                    }
                }
            }
        }
    }

    private var createProfileLinkForm: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Picker(
                UiMessages.string(.nativeSwiftPresentationType, locale: nativeUiLocale),
                selection: $viewModel.profileLinkType
            ) {
                ForEach(ProfileLinkType.allCases, id: \.self) { type in
                    Text(UiMessages.string(type.titleKey, locale: nativeUiLocale)).tag(type)
                }
            }
            .pickerStyle(.menu)

            Group {
                TextField(
                    UiMessages.string(.nativeSwiftSettingsUrl, locale: nativeUiLocale),
                    text: $viewModel.profileLinkURL
                )
                TextField(
                    UiMessages.string(.nativeSwiftSettingsHandle, locale: nativeUiLocale),
                    text: $viewModel.profileLinkHandle
                )
                TextField(
                    UiMessages.string(.nativeSwiftCommunitiesName, locale: nativeUiLocale),
                    text: $viewModel.profileLinkName
                )
                TextField(
                    UiMessages.string(.nativeSwiftSettingsImageId, locale: nativeUiLocale),
                    text: $viewModel.profileLinkImageId
                )
            }
            .textFieldStyle(.roundedBorder)

            Button(UiMessages.string(.nativeSwiftSettingsAddLink, locale: nativeUiLocale)) {
                Task { await viewModel.createProfileLink() }
            }
            .buttonStyle(.bordered)
            .disabled(viewModel.isLoading)
        }
    }
}
