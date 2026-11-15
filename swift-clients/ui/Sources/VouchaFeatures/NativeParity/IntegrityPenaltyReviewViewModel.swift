import Foundation
import Observation
import VouchaCore

enum IntegrityReviewError: Equatable {
    case loadFailed
    case loadMoreFailed
    case permissionDenied
    case reviewFailed
    case resultUncertain
    case reconciliationFailed
    case revokeFailed
}

@Observable
@MainActor
final class IntegrityPenaltyReviewViewModel {
    static let pageSize = 25

    let service: (any IntegrityPenaltyReviewServicing)?
    let viewerTier: IntegrityViewerTier
    let domain: IntegrityDomain

    var penalties: [IntegrityPenaltyRow] = []
    var committedIds: Set<String> = []
    /// Penalties revoked this session; a response that predates the revoke must not restore them.
    var removedIds: Set<String> = []
    var hasMore = false
    var isLoading = false
    var isLoadingMore = false
    var hasLoaded = false
    var permissionLost = false
    var initialError: IntegrityReviewError?
    var continuationError: IntegrityReviewError?
    var mutatingIds: Set<String> = []
    var reconciliationRequiredIds: Set<String> = []
    var errors: [String: IntegrityReviewError] = [:]

    var endCursor: String?
    var loadGeneration = 0

    init(
        service: (any IntegrityPenaltyReviewServicing)?,
        viewerTier: IntegrityViewerTier,
        domain: IntegrityDomain
    ) {
        self.service = service
        self.viewerTier = viewerTier
        self.domain = domain
    }

    var canAccess: Bool {
        viewerTier.canReviewDuePenalties(in: domain) && !permissionLost
    }

    func canConfirm(_ penalty: IntegrityPenaltyRow) -> Bool {
        canAccess && penalty.revokedAt == nil && !committedIds.contains(penalty.id)
            && !mutatingIds.contains(penalty.id) && !reconciliationRequiredIds.contains(penalty.id)
    }

    func canRevoke(_ penalty: IntegrityPenaltyRow) -> Bool {
        canAccess && viewerTier.canRevokeIntegrityPenalty && penalty.revokedAt == nil
            && !mutatingIds.contains(penalty.id) && !reconciliationRequiredIds.contains(penalty.id)
    }

    func load() async {
        guard canAccess, let service else { return }
        loadGeneration += 1
        let generation = loadGeneration
        isLoading = true
        isLoadingMore = false
        initialError = nil
        continuationError = nil
        do {
            let page = try await service.reviewDue(domain: domain, after: nil, limit: Self.pageSize)
            guard generation == loadGeneration else { return }
            // Keep reviews completed while this request was in flight; the response may predate them.
            let completed = Dictionary(
                penalties.filter { committedIds.contains($0.id) }.map { ($0.id, $0) },
                uniquingKeysWith: { first, _ in first }
            )
            let visible = page.results.filter { !removedIds.contains($0.id) }
            penalties = visible.map { completed[$0.id] ?? $0 }
            committedIds = committedIds.intersection(visible.map(\.id))
            endCursor = page.pageInfo.endCursor
            hasMore = page.pageInfo.hasNextPage
            hasLoaded = true
        } catch is CancellationError {
            guard generation == loadGeneration else { return }
        } catch {
            guard generation == loadGeneration else { return }
            initialError = failure(error, fallback: .loadFailed)
        }
        guard generation == loadGeneration else { return }
        isLoading = false
    }

    func loadMore() async {
        guard canAccess, let service, let endCursor, hasMore, !isLoading, !isLoadingMore else { return }
        let generation = loadGeneration
        isLoadingMore = true
        continuationError = nil
        do {
            let page = try await service.reviewDue(domain: domain, after: endCursor, limit: Self.pageSize)
            guard generation == loadGeneration else { return }
            var ids = Set(penalties.map(\.id))
            penalties.append(contentsOf: page.results.filter {
                !removedIds.contains($0.id) && ids.insert($0.id).inserted
            })
            self.endCursor = page.pageInfo.endCursor
            hasMore = page.pageInfo.hasNextPage
        } catch is CancellationError {
            guard generation == loadGeneration else { return }
        } catch {
            guard generation == loadGeneration else { return }
            continuationError = failure(error, fallback: .loadMoreFailed)
        }
        guard generation == loadGeneration else { return }
        isLoadingMore = false
    }

    func failure(_ error: Error, fallback: IntegrityReviewError) -> IntegrityReviewError {
        guard let error = error as? VouchaError else { return fallback }
        switch error {
        case let .api(statusCode, _), let .apiMessage(statusCode, _, _):
            guard statusCode == 401 || statusCode == 403 else { return fallback }
            permissionLost = true
            return .permissionDenied
        default:
            return fallback
        }
    }
}
