import Foundation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

extension CRMContactsViewModel {
    func load() async {
        await loadContacts(reset: true)
        if let initialContactId {
            await selectContact(id: initialContactId)
        }
    }

    func reloadContacts() async {
        await loadContacts(reset: true)
    }

    func loadMoreContacts() async {
        let canLoad = contactsPagination.lastError != nil
            || (contactsPagination.hasLoadedPage && contactsPagination.hasMore)
        guard client != nil, canLoad else { return }
        await loadContacts(reset: false)
    }

    func selectContact(id: String?) async {
        selectedContactId = id
        selectedContact = nil
        selectedSocialAccounts = []
        selectedEmails = []
        selectedNotes = []
        selectedEmailPageInfo = nil
        selectedNotePageInfo = nil
        detailErrorMessage = nil
        emailErrorMessage = nil
        noteErrorMessage = nil
        canShowArchiveConfirmation = false
        editForm.reset()
        linkedUserId = ""

        guard let id else { return }

        await loadContactDetail(contactId: id)
    }

    func loadMoreEmails() async {
        let canLoad = emailPagination.lastError != nil
            || (emailPagination.hasLoadedPage && emailPagination.hasMore)
        guard let contactId = selectedContactId, canLoad else { return }
        await loadEmails(contactId: contactId)
    }

    func loadMoreNotes() async {
        let canLoad = notePagination.lastError != nil
            || (notePagination.hasLoadedPage && notePagination.hasMore)
        guard let contactId = selectedContactId, canLoad else { return }
        await loadNotes(contactId: contactId)
    }

    func dismissArchiveConfirmation() {
        canShowArchiveConfirmation = false
    }
}

private extension CRMContactsViewModel {
    func loadContacts(reset: Bool) async {
        guard let client else { return }
        let revision = reset ? (contactsLoadRevision + 1) : contactsLoadRevision
        if reset {
            contactsLoadRevision = revision
            contactsPagination.reset()
            isLoadingList = true
            listErrorMessage = nil
        }
        guard let request = contactsPagination.beginNextPage() else { return }
        defer {
            if contactsLoadRevision == revision {
                isLoadingList = false
            }
        }

        do {
            let response: Page<CrmContact> = try await client.send(
                .crmContacts(
                    query: searchQuery.trimmed.isEmpty ? nil : searchQuery.trimmed,
                    status: selectedStatus,
                    vertical: selectedVertical,
                    linked: selectedLinkedQueryValue,
                    after: request.cursor
                )
            )
            guard contactsLoadRevision == revision, contactsPagination.isCurrent(request) else { return }
            contactsPagination.complete(
                request,
                items: response.results,
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            )
        } catch let error as VouchaError {
            guard contactsLoadRevision == revision else { return }
            contactsPagination.fail(request, error: error)
            listErrorMessage = error.errorDescription.map(UiVerbatimText.verbatim)
        } catch {
            guard contactsLoadRevision == revision else { return }
            contactsPagination.fail(request, error: .api(statusCode: 0, preconditionCode: nil))
            listErrorMessage = .verbatim(error.localizedDescription)
        }
    }

    var selectedLinkedQueryValue: Bool? {
        switch selectedLinkedFilter {
        case .all:
            nil
        case .linked:
            true
        case .unlinked:
            false
        }
    }

}
