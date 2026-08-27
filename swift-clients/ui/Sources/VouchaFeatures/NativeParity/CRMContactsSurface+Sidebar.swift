import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension CRMContactsSurface {
    func sidebarList(viewModel: CRMContactsViewModel) -> some View {
        List {
            searchSection(viewModel: viewModel)
            filterSection(viewModel: viewModel)
            createSection(viewModel: viewModel)
            importSection(viewModel: viewModel)
            contactsSection(viewModel: viewModel)
        }
    }

    private func searchSection(viewModel: CRMContactsViewModel) -> some View {
        @Bindable
        var viewModel = viewModel

        return Section(header: Text(verbatim: UiMessages.string(.nativeSwiftCommonSearch, locale: nativeUiLocale))) {
            TextField(
                UiMessages.string(.nativeSwiftCrmContactsSearchContacts, locale: nativeUiLocale),
                text: $viewModel.searchQuery
            )
            .textFieldStyle(.roundedBorder)
            .onSubmit {
                Task { await viewModel.reloadContacts() }
            }
            Button {
                Task { await viewModel.reloadContacts() }
            } label: {
                Label(
                    UiMessages.string(.nativeSwiftCommonSearch, locale: nativeUiLocale),
                    systemImage: "magnifyingglass"
                )
            }
            .buttonStyle(.borderedProminent)
        }
    }

    private func filterSection(viewModel: CRMContactsViewModel) -> some View {
        @Bindable
        var viewModel = viewModel

        return Section(header: Text(verbatim: UiMessages.string(
            .nativeSwiftCrmContactsFilters,
            locale: nativeUiLocale
        ))) {
            Picker(
                UiMessages.string(.nativeSwiftCrmContactsStatus, locale: nativeUiLocale),
                selection: $viewModel.selectedStatus
            ) {
                Text(UiMessages.string(.nativeSwiftCommonAll, locale: nativeUiLocale)).tag(nil as CrmContactStatus?)
                ForEach(CrmContactStatus.allCases, id: \.self) { status in
                    Text(UiMessages.string(status.titleKey, locale: nativeUiLocale)).tag(Optional(status))
                }
            }
            .pickerStyle(.segmented)

            Picker(
                UiMessages.string(.nativeSwiftCrmContactsVertical, locale: nativeUiLocale),
                selection: $viewModel.selectedVertical
            ) {
                Text(UiMessages.string(.nativeSwiftCommonAll, locale: nativeUiLocale)).tag(nil as CrmContactVertical?)
                ForEach(CrmContactVertical.allCases, id: \.self) { vertical in
                    Text(UiMessages.string(vertical.titleKey, locale: nativeUiLocale)).tag(Optional(vertical))
                }
            }
            .pickerStyle(.menu)

            Picker(
                UiMessages.string(.nativeSwiftCrmContactsLinked, locale: nativeUiLocale),
                selection: $viewModel.selectedLinkedFilter
            ) {
                ForEach(CRMContactsViewModel.LinkedFilter.allCases) { filter in
                    Text(UiMessages.string(filter.titleKey, locale: nativeUiLocale)).tag(filter)
                }
            }
            .pickerStyle(.segmented)
        }
    }

    private func importSection(viewModel: CRMContactsViewModel) -> some View {
        @Bindable
        var viewModel = viewModel

        return Section(header: Text(verbatim: UiMessages.string(.nativeSwiftCommonImport, locale: nativeUiLocale))) {
            TextEditor(text: $viewModel.importCsv)
                .frame(minHeight: 140)
                .overlay(
                    RoundedRectangle(cornerRadius: 8, style: .continuous)
                        .strokeBorder(.quaternary, lineWidth: 1)
                )
            Button {
                Task { await viewModel.importContacts() }
            } label: {
                Label(
                    UiMessages.string(.nativeSwiftCrmContactsImportCsv, locale: nativeUiLocale),
                    systemImage: "square.and.arrow.down"
                )
            }
            .buttonStyle(.borderedProminent)
            .disabled(viewModel.importCsv.trimmed.isEmpty || viewModel.isImporting)
            if let message = viewModel.importErrorMessage {
                Text(UiMessages.string(message, locale: nativeUiLocale))
                    .font(Typography.caption)
                    .foregroundStyle(Colors.negativeVote)
            }
        }
    }

    private func contactsSection(viewModel: CRMContactsViewModel) -> some View {
        Section(header: Text(verbatim: UiMessages.string(.nativeSwiftCrmContactsContacts, locale: nativeUiLocale))) {
            if viewModel.isLoadingList, viewModel.contacts.isEmpty {
                ProgressView()
                    .frame(maxWidth: .infinity, alignment: .center)
            } else {
                ForEach(viewModel.contacts) { contact in
                    Button {
                        Task { await viewModel.selectContact(id: contact.id) }
                    } label: {
                        contactRow(contact, isSelected: viewModel.selectedContactId == contact.id)
                    }
                    .buttonStyle(.plain)
                }
                HybridPaginationControl(
                    hasMore: viewModel.contactsPagination.hasMore,
                    isLoading: viewModel.contactsPagination.isLoading,
                    hasError: viewModel.contactsPagination.lastError != nil
                ) {
                    await viewModel.loadMoreContacts()
                }
            }
            if let message = viewModel.listErrorMessage {
                Text(UiMessages.string(message, locale: nativeUiLocale))
                    .font(Typography.caption)
                    .foregroundStyle(Colors.negativeVote)
            }
        }
    }

    private func contactRow(_ contact: CrmContact, isSelected: Bool) -> some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            HStack(alignment: .firstTextBaseline) {
                Text(contact.name)
                    .font(Typography.subheadline)
                    .foregroundStyle(Colors.primary)
                Spacer(minLength: 0)
                if contact.userId != nil {
                    Image(systemName: "person.crop.circle.badge.checkmark")
                        .foregroundStyle(Colors.secondaryLabel)
                }
            }
            Text(contact.email)
                .font(Typography.caption)
                .foregroundStyle(Colors.secondaryLabel)
                .lineLimit(1)
            HStack(spacing: Spacing.xs) {
                Text(UiMessages.string(contact.status.titleKey, locale: nativeUiLocale))
                if let vertical = contact.vertical {
                    Text(UiMessages.string(vertical.titleKey, locale: nativeUiLocale))
                }
                if let followerCount = contact.followerCount {
                    Text(UiMessages.number(followerCount, locale: nativeUiLocale))
                }
            }
            .font(Typography.caption)
            .foregroundStyle(Colors.secondaryLabel)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(Spacing.sm)
        .background(
            RoundedRectangle(cornerRadius: 8, style: .continuous)
                .fill(isSelected ? Colors.primary.opacity(0.12) : .clear)
        )
    }
}
