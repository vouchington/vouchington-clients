import Observation
import VouchaModels

@Observable
@MainActor
final class VoteIntegrityViewModel {
    let service: (any VoteIntegrityServicing)?
    let viewerTier: IntegrityViewerTier

    var flags: [VoteIntegrityFlag] = []
    var selectedStatus: IntegrityStatusFilter = .pending
    var hasMore = false
    var isLoading = false
    var isLoadingMore = false
    var initialErrorMessage: String?
    var continuationErrorMessage: String?
    var mutatingFlagIds: Set<String> = []
    var mutationErrorMessages: [String: String] = [:]
    var penalizedUserCounts: [String: Int] = [:]
    var confirmedPenaltyFlagIds: Set<String> = []
    var ambiguousPenaltyFlagIds: Set<String> = []
    var penaltyBaselineIdsByFlagId: [String: Set<String>] = [:]
    var reconciliationRequiredFlagIds: Set<String> = []
    var reconcilingFlagIds: Set<String> = []

    var endCursor: String?
    var loadGeneration = 0

    init(
        service: (any VoteIntegrityServicing)?,
        viewerTier: IntegrityViewerTier
    ) {
        self.service = service
        self.viewerTier = viewerTier
    }

    var canAccess: Bool {
        viewerTier.canReviewIntegrity
    }

}
