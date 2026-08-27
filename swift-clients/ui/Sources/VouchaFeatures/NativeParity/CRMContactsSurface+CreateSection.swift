import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension CRMContactsSurface {
    func createSection(viewModel: CRMContactsViewModel) -> some View {
        @Bindable
        var viewModel = viewModel

        return Section(header: Text(verbatim: UiMessages.string(.nativeSwiftCommonCreate, locale: nativeUiLocale))) {
            contactIdentityFields(viewModel: viewModel)
            contactClassificationFields(viewModel: viewModel)
            contactMetadataFields(viewModel: viewModel)
            createContactButton(viewModel: viewModel)
            if let message = viewModel.createErrorMessage {
                Text(UiMessages.string(message, locale: nativeUiLocale))
                    .font(Typography.caption)
                    .foregroundStyle(Colors.negativeVote)
            }
        }
    }

    private func contactIdentityFields(viewModel: CRMContactsViewModel) -> some View {
        @Bindable
        var viewModel = viewModel

        return Group {
            TextField(
                UiMessages.string(.nativeSwiftCommunitiesName, locale: nativeUiLocale),
                text: $viewModel.createForm.name
            )
            TextField(
                UiMessages.string(.nativeSwiftCrmContactsEmail, locale: nativeUiLocale),
                text: $viewModel.createForm.email
            )
            TextField(
                UiMessages.string(.nativeSwiftCrmContactsPhone, locale: nativeUiLocale),
                text: $viewModel.createForm.phone
            )
        }
        .textFieldStyle(.roundedBorder)
    }

    private func contactClassificationFields(viewModel: CRMContactsViewModel) -> some View {
        @Bindable
        var viewModel = viewModel

        return Group {
            Picker(
                UiMessages.string(.nativeSwiftCrmContactsVertical, locale: nativeUiLocale),
                selection: $viewModel.createForm.vertical
            ) {
                Text(UiMessages.string(.nativeSwiftCrmContactsNone, locale: nativeUiLocale))
                    .tag(nil as CrmContactVertical?)
                ForEach(CrmContactVertical.allCases, id: \.self) { vertical in
                    Text(UiMessages.string(vertical.titleKey, locale: nativeUiLocale)).tag(Optional(vertical))
                }
            }
            Picker(
                UiMessages.string(.nativeSwiftCrmContactsType, locale: nativeUiLocale),
                selection: $viewModel.createForm.contactType
            ) {
                ForEach(CrmContactType.allCases, id: \.self) { contactType in
                    Text(UiMessages.string(contactType.titleKey, locale: nativeUiLocale)).tag(contactType)
                }
            }
        }
    }

    private func contactMetadataFields(viewModel: CRMContactsViewModel) -> some View {
        @Bindable
        var viewModel = viewModel

        return Group {
            TextField(
                UiMessages.string(.nativeSwiftCrmContactsFollowerCount, locale: nativeUiLocale),
                text: $viewModel.createForm.followerCountText
            )
            .textFieldStyle(.roundedBorder)
            TextField(
                UiMessages.string(.nativeSwiftCrmContactsNotes, locale: nativeUiLocale),
                text: $viewModel.createForm.notes,
                axis: .vertical
            )
            .lineLimit(3 ... 6)
            TextField(
                UiMessages.string(.nativeSwiftCrmContactsAssignedUserId, locale: nativeUiLocale),
                text: $viewModel.createForm.assignedToId
            )
            .textFieldStyle(.roundedBorder)
        }
    }

    private func createContactButton(viewModel: CRMContactsViewModel) -> some View {
        Button {
            Task { await viewModel.createContact() }
        } label: {
            Label(
                UiMessages.string(.nativeSwiftCrmContactsCreateContact, locale: nativeUiLocale),
                systemImage: "person.badge.plus"
            )
        }
        .buttonStyle(.borderedProminent)
        .disabled(
            viewModel.createForm.name.trimmed.isEmpty
                || viewModel.createForm.email.trimmed.isEmpty
                || viewModel.isCreating
        )
    }
}
