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

    func load() async {
        guard canAccess, let client else { return }
        loadRevision += 1
        let revision = loadRevision
        let expectedStatus = selectedStatus
        summaryPagination.reset()
        guard let request = summaryPagination.beginNextPage() else { return }
        isLoading = true
        errorMessage = nil
        loadMoreErrorMessage = nil
        do {
            let response: ModerationAppealListResponse = try await client.send(
                .appeals(status: expectedStatus, limit: 25)
            )
            guard revision == loadRevision, expectedStatus == selectedStatus else { return }
            summaryPagination.complete(
                request,
                items: response.appeals,
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            )
        } catch is CancellationError {
            guard revision == loadRevision else { return }
            summaryPagination.cancel(request)
        } catch {
            guard revision == loadRevision, expectedStatus == selectedStatus else { return }
            summaryPagination.fail(request, error: vouchaError(from: error))
            errorMessage = message(for: error)
        }
        guard revision == loadRevision else { return }
        isLoading = false
    }

    func selectStatus(_ status: ModerationAppealStatus) async {
        guard status != selectedStatus else { return }
        selectedStatus = status
        summaryPagination.reset()
        await load()
    }

    func loadMore() async {
        guard canAccess, let client, !isLoading,
              let request = summaryPagination.beginNextPage()
        else { return }
        let revision = loadRevision
        let expectedStatus = selectedStatus
        loadMoreErrorMessage = nil
        do {
            let response: ModerationAppealListResponse = try await client.send(
                .appeals(status: expectedStatus, limit: 25, after: request.cursor)
            )
            guard revision == loadRevision, expectedStatus == selectedStatus,
                  summaryPagination.isCurrent(request)
            else { return }
            summaryPagination.complete(
                request,
                items: response.appeals,
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            )
        } catch is CancellationError {
            guard revision == loadRevision else { return }
            summaryPagination.cancel(request)
        } catch {
            guard revision == loadRevision, expectedStatus == selectedStatus else { return }
            summaryPagination.fail(request, error: vouchaError(from: error))
            loadMoreErrorMessage = message(for: error)
        }
    }

    private func vouchaError(from error: Error) -> VouchaError {
        (error as? VouchaError) ?? .api(statusCode: 0, preconditionCode: nil)
    }
}
