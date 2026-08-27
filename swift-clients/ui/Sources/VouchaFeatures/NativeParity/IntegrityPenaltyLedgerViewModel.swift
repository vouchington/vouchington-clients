import Observation
import VouchaModels

enum IntegrityPenaltyStatusFilter: String, CaseIterable {
    case active, revoked, all

    var apiStatus: IntegrityPenaltyStatus? {
        switch self {
        case .active: .active
        case .revoked: .revoked
        case .all: nil
        }
    }
}

enum IntegrityLedgerError: Equatable {
    case loadFailed
    case loadMoreFailed
    case reportUnavailable
    case scopeUnavailable
    case revokeFailed
    case resultUncertain
    case reconciliationFailed
}

@Observable
@MainActor
final class IntegrityPenaltyLedgerViewModel {
    let service: (any IntegrityPenaltyServicing)?
    let viewerTier: IntegrityViewerTier
    let domain: IntegrityDomain

    var penalties: [IntegrityPenaltyRow] = []
    var selectedStatus: IntegrityPenaltyStatusFilter = .active
    var hasMore = false
    var isLoading = false
    var isLoadingMore = false
    var initialError: IntegrityLedgerError?
    var continuationError: IntegrityLedgerError?
    var mutatingPenaltyIds: Set<String> = []
    var reconcilingPenaltyIds: Set<String> = []
    var reconciliationRequiredIds: Set<String> = []
    var mutationErrors: [String: IntegrityLedgerError] = [:]

    var endCursor: String?
    var loadGeneration = 0

    init(
        service: (any IntegrityPenaltyServicing)?,
        viewerTier: IntegrityViewerTier,
        domain: IntegrityDomain
    ) {
        self.service = service
        self.viewerTier = viewerTier
        self.domain = domain
    }

    var canAccess: Bool {
        viewerTier.canReviewIntegrityPenalties
    }

}
