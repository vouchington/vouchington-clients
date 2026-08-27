import Foundation
import Observation
import VouchaAPI
import VouchaLocalization
import VouchaModels

enum StaffSupportMode {
    case threads
    case contacts
}

@Observable
@MainActor
final class StaffSupportViewModel {
    struct ListCursorProvenance: Equatable {
        let query: String?
        let status: StaffSupportThreadStatusFilter?
    }

    struct ThreadSelection {
        let id: String
        let generation: Int
        let errorOperationGeneration: Int
    }

    struct ThreadLoadResult {
        let selection: ThreadSelection?
        let didLoad: Bool
    }

    struct ContactSelection {
        let id: String
        let generation: Int
    }

    let client: APIClient?
    let administratorId: String?
    let mode: StaffSupportMode
    private let initialThreadId: String?
    private let initialContactId: String?
    @ObservationIgnored
    var threadSelectionGeneration = 0
    @ObservationIgnored
    var contactSelectionGeneration = 0
    @ObservationIgnored
    var listLoadGeneration = 0
    @ObservationIgnored
    var activeThreadSelection: ThreadSelection?
    @ObservationIgnored
    var activeContactSelection: ContactSelection?
    @ObservationIgnored
    var draftPollingTask: Task<Bool, Error>?
    @ObservationIgnored
    var retryOperation: RetryOperation?
    @ObservationIgnored
    var errorOperationGeneration = 0

    var query = ""
    var status: StaffSupportThreadStatusFilter?
    var threads: [SupportThread] = []
    var threadPageInfo: Page<SupportThread>.PageInfo?
    @ObservationIgnored
    var threadPageCursorProvenance: ListCursorProvenance?
    var contacts: [SupportContact] = []
    var contactPageInfo: Page<SupportContact>.PageInfo?
    @ObservationIgnored
    var contactPageCursorProvenance: ListCursorProvenance?
    var selectedThread: SupportThread?
    var messages: [SupportMessage] = []
    var messagePageInfo: Page<SupportMessage>.PageInfo?
    var selectedContact: SupportContact?
    var contactThreads: [SupportThread] = []
    var contactThreadPageInfo: Page<SupportThread>.PageInfo?
    var replyText = ""
    var draftEdits: [String: String] = [:]
    var errorMessage: UiVerbatimText?
    var isListLoading = false
    var isDetailLoading = false
    var isMutating = false
    var isWaitingForDraft = false

    init(client: APIClient?, administratorId: String?, mode: StaffSupportMode, routeMatch: NativeRouteMatch?) {
        self.client = client
        self.administratorId = administratorId
        self.mode = mode
        initialThreadId = routeMatch?.param("threadId")
        initialContactId = routeMatch?.param("contactId")
    }

    func load() async {
        await reloadList()
        if let initialThreadId {
            await selectThread(initialThreadId)
        }
        if let initialContactId {
            await selectContact(initialContactId)
        }
    }

    func draftText(for message: SupportMessage) -> String {
        draftEdits[message.id] ?? message.bodyText
    }

    func isDraftDirty(_ message: SupportMessage) -> Bool {
        draftText(for: message) != message.bodyText
    }

    var canMutateSelectedThreadDrafts: Bool {
        selectedThread?.status == .open || selectedThread?.status == .assigned
    }

    var canGenerateDraft: Bool {
        !messages.contains { $0.direction == .outbound && $0.draftedAt != nil && $0.sentAt == nil }
            && (messages.contains(where: { $0.direction == .inbound }) || messagePageInfo?.hasNextPage == true)
    }

    var isLoading: Bool {
        isListLoading || isDetailLoading
    }

    func cancelDraftPolling() {
        draftPollingTask?.cancel()
        draftPollingTask = nil
    }

    func setDraftText(_ value: String, messageId: String) {
        draftEdits[messageId] = value
    }

    func beginThreadSelection(
        _ id: String,
        errorOperationGeneration: Int,
        preservingComposerState: Bool = false
    ) -> ThreadSelection {
        cancelDraftPolling()
        threadSelectionGeneration += 1
        let selection = ThreadSelection(
            id: id,
            generation: threadSelectionGeneration,
            errorOperationGeneration: errorOperationGeneration
        )
        activeThreadSelection = selection
        selectedThread = nil
        messages = []
        messagePageInfo = nil
        if !preservingComposerState {
            replyText = ""
            draftEdits = [:]
        }
        return selection
    }

}
