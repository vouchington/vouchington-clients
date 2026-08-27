import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct ProfileLinkEditor: View {
    @Environment(\.locale)
    var nativeUiLocale
    let link: VouchaModels.ProfileLink
    @Bindable
    var viewModel: SettingsViewModel
    @State
    private var draft: Draft

    init(link: VouchaModels.ProfileLink, viewModel: SettingsViewModel) {
        self.link = link
        self.viewModel = viewModel
        _draft = State(initialValue: Draft(link: link))
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            HStack {
                VStack(alignment: .leading, spacing: 2) {
                    Text(link.name ?? link.handle ?? link.url
                        ?? UiMessages.string(link.linkType.titleKey, locale: nativeUiLocale))
                    Text(UiMessages.string(link.linkType.titleKey, locale: nativeUiLocale))
                        .font(Typography.caption)
                        .foregroundStyle(Colors.secondaryLabel)
                }
                Spacer(minLength: 0)
                Button {
                    viewModel.moveProfileLink(id: link.id, by: -1)
                    Task { await viewModel.reorderProfileLinks() }
                } label: {
                    Image(systemName: "arrow.up")
                }
                .buttonStyle(.plain)
                .disabled(viewModel.isLoading)
                Button {
                    viewModel.moveProfileLink(id: link.id, by: 1)
                    Task { await viewModel.reorderProfileLinks() }
                } label: {
                    Image(systemName: "arrow.down")
                }
                .buttonStyle(.plain)
                .disabled(viewModel.isLoading)
                Button(UiMessages.string(.nativeSwiftCommonDelete, locale: nativeUiLocale), role: .destructive) {
                    Task { await viewModel.deleteProfileLink(id: link.id) }
                }
                .buttonStyle(.bordered)
            }

            TextField(UiMessages.string(.nativeSwiftSettingsUrl, locale: nativeUiLocale), text: $draft.url)
                .textFieldStyle(.roundedBorder)
            TextField(UiMessages.string(.nativeSwiftSettingsHandle, locale: nativeUiLocale), text: $draft.handle)
                .textFieldStyle(.roundedBorder)
            TextField(UiMessages.string(.nativeSwiftCommunitiesName, locale: nativeUiLocale), text: $draft.name)
                .textFieldStyle(.roundedBorder)
            TextField(UiMessages.string(.nativeSwiftSettingsImageId, locale: nativeUiLocale), text: $draft.imageId)
                .textFieldStyle(.roundedBorder)

            Button(UiMessages.string(.nativeSwiftCommonSave, locale: nativeUiLocale)) {
                Task {
                    await viewModel.updateProfileLink(
                        id: link.id,
                        url: draft.url.isEmpty ? nil : draft.url,
                        handle: draft.handle.isEmpty ? nil : draft.handle,
                        name: draft.name.isEmpty ? nil : draft.name,
                        imageId: draft.imageId.isEmpty ? nil : draft.imageId
                    )
                }
            }
            .buttonStyle(.bordered)
            .disabled(viewModel.isLoading)
        }
        .padding(Spacing.sm)
        .overlay(
            RoundedRectangle(cornerRadius: 8, style: .continuous)
                .strokeBorder(.quaternary, lineWidth: 1)
        )
        .onChange(of: link.updatedAt) { _, _ in
            draft = Draft(link: link)
        }
    }

    private struct Draft {
        var url: String
        var handle: String
        var name: String
        var imageId: String

        init(link: VouchaModels.ProfileLink) {
            url = link.url ?? ""
            handle = link.handle ?? ""
            name = link.name ?? ""
            imageId = link.imageId ?? ""
        }
    }
}
