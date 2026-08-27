import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

extension CRMContactsSurface {
    func accountLinkSection(viewModel: CRMContactsViewModel) -> some View {
        @Bindable
        var viewModel = viewModel

        return Section(header: Text(verbatim: UiMessages.string(
            .nativeSwiftCrmContactsAccountLink,
            locale: nativeUiLocale
        ))) {
            TextField(
                UiMessages.string(.nativeSwiftCrmContactsLinkedUserId, locale: nativeUiLocale),
                text: $viewModel.linkedUserId
            )
            .textFieldStyle(.roundedBorder)
            HStack {
                Button {
                    Task { await viewModel.linkSelectedContactToUser() }
                } label: {
                    Label(
                        UiMessages.string(.nativeSwiftCrmContactsLinkAccount, locale: nativeUiLocale),
                        systemImage: "link"
                    )
                }
                .buttonStyle(.bordered)
                .disabled(viewModel.linkedUserId.trimmed.isEmpty || viewModel.isLinking)

                Button {
                    Task { await viewModel.unlinkSelectedContactFromUser() }
                } label: {
                    Label(
                        UiMessages.string(.nativeSwiftCrmContactsUnlink, locale: nativeUiLocale),
                        systemImage: "link.slash"
                    )
                }
                .buttonStyle(.bordered)
                .disabled(viewModel.selectedContactOrNull?.userId == nil || viewModel.isLinking)
            }
            linkedUserCaption(viewModel: viewModel)
        }
    }

    @ViewBuilder
    private func linkedUserCaption(viewModel: CRMContactsViewModel) -> some View {
        if let userId = viewModel.selectedContactOrNull?.userId {
            Text(UiMessages.string(
                .nativeSwiftCrmContactsLinkedUser,
                parameters: ["id": UiMessages.string(.verbatim(userId), locale: nativeUiLocale)],
                locale: nativeUiLocale
            ))
            .font(Typography.caption)
            .foregroundStyle(Colors.secondaryLabel)
        }
    }

    func socialAccountsSection(viewModel: CRMContactsViewModel) -> some View {
        Section(header: Text(verbatim: UiMessages.string(
            .nativeSwiftCrmContactsSocialAccounts,
            locale: nativeUiLocale
        ))) {
            ForEach(viewModel.selectedSocialAccounts) { account in
                VStack(alignment: .leading, spacing: Spacing.xs) {
                    Text(UiMessages.string(account.platform.titleText, locale: nativeUiLocale))
                    Text(account.handle)
                        .font(Typography.caption)
                        .foregroundStyle(Colors.secondaryLabel)
                }
            }
            if viewModel.selectedSocialAccounts.isEmpty {
                Text(UiMessages.string(.nativeSwiftCrmContactsNoLinkedSocialAccounts, locale: nativeUiLocale))
                    .foregroundStyle(Colors.secondaryLabel)
            }
        }
    }
}
