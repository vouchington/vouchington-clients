import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension CRMContactsSurface {
    func detailList(viewModel: CRMContactsViewModel) -> some View {
        List {
            selectedContactSection(viewModel: viewModel)

            if viewModel.selectedContactOrNull != nil {
                accountLinkSection(viewModel: viewModel)
                socialAccountsSection(viewModel: viewModel)
                emailHistorySection(viewModel: viewModel)
                notesSection(viewModel: viewModel)
            }
        }
        .crmArchiveConfirmation(viewModel: viewModel)
    }

    private func selectedContactSection(viewModel: CRMContactsViewModel) -> some View {
        @Bindable
        var viewModel = viewModel

        return Section(header: Text(verbatim: UiMessages.string(
            .nativeSwiftCrmContactsDetail,
            locale: nativeUiLocale
        ))) {
            if let contact = viewModel.selectedContactOrNull {
                contactSummary(contact)
                editContactFields(viewModel: viewModel)
                contactActionButtons(viewModel: viewModel)
                if let message = viewModel.detailErrorMessage {
                    Text(UiMessages.string(message, locale: nativeUiLocale))
                        .font(Typography.caption)
                        .foregroundStyle(Colors.negativeVote)
                }
            } else if viewModel.isLoadingDetail {
                ProgressView()
                    .frame(maxWidth: .infinity, alignment: .center)
            } else {
                EmptyStateView(
                    icon: "person.text.rectangle",
                    title: .message(.nativeSwiftEmptyStateCrm),
                    message: .message(.nativeSwiftEmptyStateCrmSelectContactMessage)
                )
            }
        }
    }

    private func contactSummary(_ contact: CrmContact) -> some View {
        Group {
            Text(contact.name)
                .font(Typography.headline)
            Text(contact.email)
                .foregroundStyle(Colors.secondaryLabel)
            Text(UiMessages.string(contact.status.titleKey, locale: nativeUiLocale))
                .font(Typography.caption)
                .foregroundStyle(Colors.secondaryLabel)
        }
    }

    private func editContactFields(viewModel: CRMContactsViewModel) -> some View {
        @Bindable
        var viewModel = viewModel

        return Group {
            TextField(
                UiMessages.string(.nativeSwiftCommunitiesName, locale: nativeUiLocale),
                text: $viewModel.editForm.name
            )
            TextField(
                UiMessages.string(.nativeSwiftCrmContactsEmail, locale: nativeUiLocale),
                text: $viewModel.editForm.email
            )
            TextField(
                UiMessages.string(.nativeSwiftCrmContactsPhone, locale: nativeUiLocale),
                text: $viewModel.editForm.phone
            )
            Picker(
                UiMessages.string(.nativeSwiftCrmContactsVertical, locale: nativeUiLocale),
                selection: $viewModel.editForm.vertical
            ) {
                Text(UiMessages.string(.nativeSwiftCrmContactsNone, locale: nativeUiLocale))
                    .tag(nil as CrmContactVertical?)
                ForEach(CrmContactVertical.allCases, id: \.self) { vertical in
                    Text(UiMessages.string(vertical.titleKey, locale: nativeUiLocale)).tag(Optional(vertical))
                }
            }
            Picker(
                UiMessages.string(.nativeSwiftCrmContactsType, locale: nativeUiLocale),
                selection: $viewModel.editForm.contactType
            ) {
                ForEach(CrmContactType.allCases, id: \.self) { contactType in
                    Text(UiMessages.string(contactType.titleKey, locale: nativeUiLocale)).tag(contactType)
                }
            }
            TextField(
                UiMessages.string(.nativeSwiftCrmContactsFollowerCount, locale: nativeUiLocale),
                text: $viewModel.editForm.followerCountText
            )
            TextField(
                UiMessages.string(.nativeSwiftCrmContactsNotes, locale: nativeUiLocale),
                text: $viewModel.editForm.notes,
                axis: .vertical
            )
            .lineLimit(3 ... 6)
            TextField(
                UiMessages.string(.nativeSwiftCrmContactsAssignedUserId, locale: nativeUiLocale),
                text: $viewModel.editForm.assignedToId
            )
        }
        .textFieldStyle(.roundedBorder)
    }

    private func contactActionButtons(viewModel: CRMContactsViewModel) -> some View {
        @Bindable
        var viewModel = viewModel

        return HStack {
            Button {
                Task { await viewModel.saveSelectedContact() }
            } label: {
                Label(
                    UiMessages.string(.nativeSwiftCommonSave, locale: nativeUiLocale),
                    systemImage: "square.and.arrow.down"
                )
            }
            .buttonStyle(.borderedProminent)
            .disabled(viewModel.isSaving)

            Button(role: .destructive) {
                viewModel.canShowArchiveConfirmation = true
            } label: {
                Label(UiMessages.string(.nativeSwiftCommonArchive, locale: nativeUiLocale), systemImage: "archivebox")
            }
            .buttonStyle(.bordered)
            .disabled(viewModel.isArchiving)
        }
    }

}
