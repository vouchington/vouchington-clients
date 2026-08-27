import VouchaAPI
import VouchaModels

extension CRMContactsViewModel {
    var contacts: [CrmContact] {
        get { contactsPagination.items }
        set { contactsPagination.replaceItems(newValue) }
    }

    var contactsPageInfo: Page<CrmContact>.PageInfo? {
        get {
            guard contactsPagination.hasLoadedPage else { return nil }
            return .init(hasNextPage: contactsPagination.hasMore, endCursor: contactsPagination.endCursor)
        }
        set {
            guard let newValue else {
                contactsPagination.reset(items: contacts)
                return
            }
            contactsPagination.restoreContinuation(
                endCursor: newValue.endCursor,
                hasMore: newValue.hasNextPage
            )
        }
    }

    var selectedEmails: [CrmMessage] {
        get { emailPagination.items }
        set { emailPagination.replaceItems(newValue) }
    }

    var selectedEmailPageInfo: Page<CrmMessage>.PageInfo? {
        get {
            guard emailPagination.hasLoadedPage else { return nil }
            return .init(hasNextPage: emailPagination.hasMore, endCursor: emailPagination.endCursor)
        }
        set {
            guard let newValue else {
                emailPagination.reset(items: selectedEmails)
                return
            }
            emailPagination.restoreContinuation(endCursor: newValue.endCursor, hasMore: newValue.hasNextPage)
        }
    }

    var selectedNotes: [CrmNote] {
        get { notePagination.items }
        set { notePagination.replaceItems(newValue) }
    }

    var selectedNotePageInfo: Page<CrmNote>.PageInfo? {
        get {
            guard notePagination.hasLoadedPage else { return nil }
            return .init(hasNextPage: notePagination.hasMore, endCursor: notePagination.endCursor)
        }
        set {
            guard let newValue else {
                notePagination.reset(items: selectedNotes)
                return
            }
            notePagination.restoreContinuation(endCursor: newValue.endCursor, hasMore: newValue.hasNextPage)
        }
    }

    var canLoadMoreContacts: Bool {
        contactsPagination.hasMore && !contactsPagination.isLoading
    }

    var canLoadMoreEmails: Bool {
        emailPagination.hasMore && !emailPagination.isLoading && !isLoadingDetail
    }

}
