import Foundation
import Observation
import VouchaAPI
import VouchaLocalization
import VouchaModels

enum NativeReviewQueueState: Equatable {
    case signInRequired
    case administratorRequired
    case idle
    case loading
    case loaded
    case empty
    case error
}

struct NativeReviewQueueItem: Identifiable {
    let post: AdminReviewQueuePost
    var clearanceStatus: AdminReviewQueueClearanceStatus

    var id: String {
        post.id
    }
}

enum NativeReviewQueueAction {
    case approve
    case reject
    case reReview

    var status: AdminReviewQueueClearanceStatus {
        switch self {
        case .approve: .approved
        case .reject: .rejected
        case .reReview: .inReview
        }
    }
}

@Observable
@MainActor
final class NativeReviewQueueViewModel {
    let loadTaskId = UUID()
    var pagination = CursorPaginationState<NativeReviewQueueItem>()
    var items: [NativeReviewQueueItem] {
        get { pagination.items }
        set { pagination.replaceItems(newValue) }
    }

    var state: NativeReviewQueueState
    var errorMessage: UiVerbatimText?
    var hasNextPage: Bool {
        get { pagination.hasLoadedPage && pagination.hasMore }
        set { pagination.restoreContinuation(endCursor: pagination.endCursor, hasMore: newValue) }
    }

    var endCursor: String? {
        get { pagination.endCursor }
        set { pagination.restoreContinuation(endCursor: newValue, hasMore: pagination.hasMore) }
    }

    var isListLoading = false
    var inFlightPostIds: Set<String> = []
    var exposureState: ModerationExposureState?
    var exposureIsStale = true
    var revealedPostIds: Set<String> = []
    var inFlightRevealPostId: String?
    @ObservationIgnored
    var cooldownRefetchTask: Task<Void, Never>?
    @ObservationIgnored
    var cooldownRefetchRevision = 0
    @ObservationIgnored
    var exposureRequestRevision = 0
    @ObservationIgnored
    var exposureOutcomeRevision = 0

    let client: APIClient?
    let cooldownSleep: @Sendable (UInt64) async throws -> Void
    let isSignedIn: Bool
    let isAdministrator: Bool
    var listGeneration = 0

    init(
        client: APIClient?,
        isSignedIn: Bool,
        isAdministrator: Bool,
        cooldownSleep: @escaping @Sendable (UInt64) async throws -> Void = {
            try await Task.sleep(nanoseconds: $0)
        }
    ) {
        self.client = client
        self.cooldownSleep = cooldownSleep
        self.isSignedIn = isSignedIn
        self.isAdministrator = isAdministrator
        state = if !isSignedIn {
            .signInRequired
        } else if !isAdministrator {
            .administratorRequired
        } else {
            .idle
        }
    }

    deinit {
        cooldownRefetchTask?.cancel()
    }

    var canLoadMore: Bool {
        hasNextPage && !pagination.isLoading && inFlightPostIds.isEmpty && isAuthorized
    }

    var canRefresh: Bool {
        !isListLoading && inFlightPostIds.isEmpty && isAuthorized
    }

    func isMutating(postId: String) -> Bool {
        inFlightPostIds.contains(postId)
    }

    func actionsAreDisabled(for postId: String) -> Bool {
        isListLoading || inFlightPostIds.contains(postId)
    }

    var hasScheduledCooldownRefetch: Bool {
        cooldownRefetchTask != nil
    }

    var isAuthorized: Bool {
        isSignedIn && isAdministrator && client != nil
    }
}
