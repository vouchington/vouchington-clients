import Foundation
import Observation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

@Observable
@MainActor
final class ModerationAppealsViewModel {
    let client: APIClient?
    let isSignedIn: Bool
    let isAdministrator: Bool
    let isSiteModerator: Bool
    let rerunPollAttempts: Int
    let rerunPollDelay: @Sendable () async throws -> Void
    let admissionStore: AppealAIRerunAdmissionStore
    let admissionOwner: AppealAIRerunOwner?

    var detailModerationAppeal: ModerationAppeal?
    var isLoadingDetail = false
    var detailError: UiVerbatimText?
    var detailRevision = 0
    var selectedDetailId: String?

    var summaryPagination = CursorPaginationState<ModerationAppealSummary>()

    var appealPagination = CursorPaginationState<ModerationAppeal>()
    var appeals: [ModerationAppeal] {
        get { appealPagination.items }
        set { appealPagination.replaceItems(newValue) }
    }

    var hasMore: Bool {
        get { summaryPagination.hasMore }
        set { summaryPagination.restoreContinuation(endCursor: summaryPagination.endCursor, hasMore: newValue) }
    }

    var isLoading = false
    var isLoadingMore: Bool {
        summaryPagination.isLoading && summaryPagination.hasLoadedPage
    }

    var mutatingAppealId: String?
    var mutationAdmissionToken: UUID?
    var errorMessage: UiVerbatimText?
    var loadMoreErrorMessage: UiVerbatimText?
    var ambiguousDeliveryAppealIds: Set<String> = []
    var pendingAIReruns: [String: AppealAIRerunBaseline] {
        admissionStore.pending(for: admissionOwner)
    }

    var aiRerunTasks: [String: Task<Void, Never>] = [:]
    var aiRerunTaskTokens: [String: UUID] = [:]
    var selectedStatus: ModerationAppealStatus = .pending
    var drafts: [String: String] = [:]
    var locallyEditedDraftAppealIds: Set<String> = []

    var loadRevision = 0
    var targetedOutcomeRevision = 0

    init(
        client: APIClient?,
        isSignedIn: Bool,
        isAdministrator: Bool,
        isSiteModerator: Bool,
        currentUserId: String? = nil,
        admissionStore: AppealAIRerunAdmissionStore? = nil,
        rerunPollAttempts: Int = 20,
        rerunPollDelay: @escaping @Sendable () async throws -> Void = {
            try await Task.sleep(for: .seconds(3))
        }
    ) {
        self.client = client
        self.admissionStore = admissionStore ?? (currentUserId == nil
            ? AppealAIRerunAdmissionStore() : .session)
        admissionOwner = AppealAIRerunOwner(
            userId: currentUserId ?? (admissionStore == nil ? UUID().uuidString : nil),
            baseURL: client?.baseURL
        )
        self.isSignedIn = isSignedIn
        self.isAdministrator = isAdministrator
        self.isSiteModerator = isSiteModerator
        self.rerunPollAttempts = rerunPollAttempts
        self.rerunPollDelay = rerunPollDelay
    }

    var canAccess: Bool {
        isSignedIn && (isAdministrator || isSiteModerator)
    }

    var isMutating: Bool {
        mutatingAppealId != nil
    }

}
