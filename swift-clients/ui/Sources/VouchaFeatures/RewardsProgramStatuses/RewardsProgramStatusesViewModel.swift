import Foundation
import Observation
import VouchaAPI
import VouchaLocalization
import VouchaModels

@Observable
@MainActor
final class RewardsProgramStatusesViewModel {
    var pagination = CursorPaginationState<RewardsProgramStatus>()
    var statuses: [RewardsProgramStatus] {
        get { pagination.items }
        set { pagination.replaceItems(reconciled(newValue)) }
    }

    var topicQuery = "" {
        didSet {
            guard topicQuery != oldValue else { return }
            searchGeneration = UUID()
            topicResults = []
            searchErrorMessage = nil
            isSearching = false
        }
    }

    var topicResults: [TopicSearchResult] = []
    var isLoading = false
    var isSearching = false
    var isCreating = false
    var mutatingIds: Set<String> = []
    var errorMessage: UiVerbatimText?
    var searchErrorMessage: UiVerbatimText?
    var mutationErrorMessage: UiVerbatimText?
    var continuationErrorMessage: UiVerbatimText? {
        guard pagination.hasLoadedPage, pagination.lastError != nil, errorMessage == nil else { return nil }
        return .message(.nativeSwiftCommonTryAgain)
    }

    let service: any RewardsProgramStatusServicing
    var localUpserts: [String: PendingLocalUpsert] = [:]
    var pendingDeletions: [String: PendingLocalDeletion] = [:]
    var inFlightReadIds: Set<UUID> = []
    var searchGeneration = UUID()

    init(service: any RewardsProgramStatusServicing) {
        self.service = service
    }
}
