import Foundation
import Observation
import VouchaAPI
import VouchaLocalization
import VouchaModels

@Observable
@MainActor
final class CRMContactFormState {
    var name = ""
    var email = ""
    var phone = ""
    var notes = ""
    var assignedToId = ""
    var followerCountText = ""
    var vertical: CrmContactVertical?
    var contactType: CrmContactType = .influencer

    func reset() {
        name = ""
        email = ""
        phone = ""
        notes = ""
        assignedToId = ""
        followerCountText = ""
        vertical = nil
        contactType = .influencer
    }

    func populate(from contact: CrmContact) {
        name = contact.name
        email = contact.email
        phone = contact.phone ?? ""
        notes = contact.notes ?? ""
        assignedToId = contact.assignedToId ?? ""
        followerCountText = contact.followerCount.map(String.init) ?? ""
        vertical = contact.vertical
        contactType = contact.contactType
    }

    func followerCountValue() -> Int? {
        let trimmed = followerCountText.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return nil }
        return Int(trimmed)
    }
}

@Observable
@MainActor
final class CRMContactsViewModel {
    enum LinkedFilter: String, CaseIterable, Identifiable {
        case all, linked, unlinked

        var id: String {
            rawValue
        }
    }

    let client: APIClient?
    let initialContactId: String?
    var contactsLoadRevision = 0
    var detailLoadRevision = 0

    var contactsPagination = CursorPaginationState<CrmContact>()
    var emailPagination = CursorPaginationState<CrmMessage>()
    var notePagination = CursorPaginationState<CrmNote>()

    var searchQuery = ""
    var selectedStatus: CrmContactStatus?
    var selectedVertical: CrmContactVertical?
    var selectedLinkedFilter: LinkedFilter = .all
    var selectedContactId: String?
    var selectedContact: CrmContact?
    var selectedSocialAccounts: [CrmContactSocialAccount] = []

    var listErrorMessage: UiVerbatimText?
    var detailErrorMessage: UiVerbatimText?
    var createErrorMessage: UiVerbatimText?
    var emailErrorMessage: UiVerbatimText?
    var noteErrorMessage: UiVerbatimText?
    var importErrorMessage: UiVerbatimText?
    var isLoadingList = false
    var isLoadingDetail = false
    var isCreating = false
    var isSaving = false
    var isArchiving = false
    var isLinking = false
    var isSendingEmail = false
    var isDraftingEmail = false
    var isLoadingMoreEmails = false
    var isCreatingNote = false
    var isImporting = false
    var canShowArchiveConfirmation = false
    var createForm = CRMContactFormState()
    var editForm = CRMContactFormState()
    var emailSubject = ""
    var emailBodyText = ""
    var emailBodyHtml = ""
    var emailDraftPrompt = ""
    var emailDraftTone = ""
    var emailDraftGeneratedAt: Date?
    var emailProvider: CrmEmailProvider = .ses
    var emailCtaUrl = ""
    var noteBody = ""
    var importCsv = "name,email\n"
    var importSessionFactory: () -> URLSession = { URLSession(configuration: .default) }
    var linkedUserId = ""

    init(client: APIClient?, routeMatch: NativeRouteMatch?) {
        self.client = client
        initialContactId = routeMatch?.param("contactId")
        selectedContactId = initialContactId
        searchQuery = routeMatch?.queryValue("q") ?? ""
        selectedStatus = routeMatch?.queryValue("status").flatMap(CrmContactStatus.init(rawValue:))
        selectedVertical = routeMatch?.queryValue("vertical").flatMap(CrmContactVertical.init(rawValue:))
        selectedLinkedFilter = switch routeMatch?.queryValue("linked") {
        case "true":
            .linked
        case "false":
            .unlinked
        default:
            .all
        }
    }

    var selectedContactOrNull: CrmContact? {
        if let selectedContact {
            selectedContact
        } else if let selectedContactId {
            contacts.first { $0.id == selectedContactId }
        } else {
            nil
        }
    }

}
