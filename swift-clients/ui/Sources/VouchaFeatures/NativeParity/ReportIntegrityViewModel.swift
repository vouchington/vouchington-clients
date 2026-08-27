import Observation
import VouchaModels

@Observable
@MainActor
final class ReportIntegrityViewModel {
    let service: (any ReportIntegrityServicing)?
    let viewerTier: IntegrityViewerTier

    var flags: [ReportIntegrityFlag] = []
    var selectedStatus: IntegrityStatusFilter = .pending
    var hasMore = false
    var isLoading = false
    var isLoadingMore = false
    var initialErrorMessage: String?
    var continuationErrorMessage: String?
    var mutatingFlagIds: Set<String> = []
    var mutationErrorMessages: [String: String] = [:]
    var penalizedReporterCounts: [String: Int] = [:]
    var reconciliationRequiredFlagIds: Set<String> = []
    var reconcilingFlagIds: Set<String> = []

    var endCursor: String?
    var loadGeneration = 0

    init(
        service: (any ReportIntegrityServicing)?,
        viewerTier: IntegrityViewerTier
    ) {
        self.service = service
        self.viewerTier = viewerTier
    }

    var canAccess: Bool {
        viewerTier.canReviewIntegrity
    }

}
