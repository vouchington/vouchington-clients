import Foundation
import Observation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

public enum ModerationReportsViewerTier: Hashable, Sendable {
    case member, siteModerator, administrator

    var isStaff: Bool {
        self != .member
    }

    var canRemove: Bool {
        self == .administrator
    }
}

public enum ModerationReportsMode: String, CaseIterable, Sendable {
    case grouped, flat
}

struct ModerationReportsPageToken: Identifiable {
    let id: String
}

@Observable
@MainActor
public final class ModerationReportsViewModel {
    public internal(set) var memberReports: [MemberModerationReport] = []
    public internal(set) var staffReports: [StaffModerationReport] = []
    public internal(set) var clusters: [StaffModerationReportEntityCluster] = []
    public internal(set) var duplicateClusters: [StaffModerationReportDuplicateCluster] = []
    public internal(set) var selectedReportIds: Set<String> = []
    public internal(set) var inFlightReportIds: Set<String> = []
    public internal(set) var status: ModerationReportStatus = .pending
    public internal(set) var sort: ModerationReportSort
    public internal(set) var mode: ModerationReportsMode
    var reportPagination = CursorPaginationState<ModerationReportsPageToken>()
    public internal(set) var isLoading = false
    public var isLoadingMore: Bool {
        reportPagination.isLoading && reportPagination.hasLoadedPage
    }

    public internal(set) var isMutatingQueue = false
    public internal(set) var isRefreshingGrouped = false
    public internal(set) var notice: UiVerbatimText?
    public internal(set) var loadError: UiVerbatimText?
    public internal(set) var paginationError: UiVerbatimText?
    public internal(set) var actionErrors: [String: UiVerbatimText] = [:]
    public internal(set) var endCursor: String? {
        get { reportPagination.endCursor }
        set { reportPagination.restoreContinuation(endCursor: newValue, hasMore: reportPagination.hasMore) }
    }

    public internal(set) var hasNextPage: Bool {
        get { reportPagination.hasLoadedPage && reportPagination.hasMore }
        set { reportPagination.restoreContinuation(endCursor: reportPagination.endCursor, hasMore: newValue) }
    }

    var pendingConfirmation: ModerationReportsConfirmation?

    let client: APIClient?
    public let viewerTier: ModerationReportsViewerTier
    var generation = 0
    @ObservationIgnored
    var activeReportPageRequest: CursorPageRequest?
    var groupedLoadedPageCount = 0
    private var evictedReportIds: Set<String> = []

    public init(client: APIClient?, viewerTier: ModerationReportsViewerTier) {
        self.client = client
        self.viewerTier = viewerTier
        mode = viewerTier.isStaff ? .grouped : .flat
        sort = viewerTier.isStaff ? .severity : .createdAtDesc
    }

    public var isReadOnly: Bool {
        !viewerTier.isStaff
    }

    public var canLoadMore: Bool {
        hasNextPage && endCursor != nil && !isLoadingMore && !isQueueActionInProgress
    }

    public var isQueueActionInProgress: Bool {
        isMutatingQueue || isRefreshingGrouped
    }

    public func toggleSelection(_ reportId: String) {
        guard viewerTier.isStaff, !isQueueActionInProgress, !inFlightReportIds.contains(reportId) else { return }
        if selectedReportIds.contains(reportId) {
            selectedReportIds.remove(reportId)
        } else {
            selectedReportIds.insert(reportId)
        }
    }

    public func replaceSelectionWithActiveRemovableReports(
        in duplicateCluster: StaffModerationReportDuplicateCluster
    ) {
        guard viewerTier.canRemove, !isQueueActionInProgress else { return }
        let reports = duplicateCluster.clusters.flatMap(\.reports).filter {
            isActive($0) && actionIsAllowed(.removeTarget, for: $0)
        }
        selectedReportIds = Set(reports.map(\.id))
    }

    func report(id: String) -> StaffModerationReport? {
        guard !evictedReportIds.contains(id) else { return nil }
        return staffReports.first { $0.id == id }
            ?? clusters.lazy.flatMap(\.reports).first { $0.id == id }
            ?? duplicateClusters.lazy.flatMap(\.clusters).flatMap(\.reports).first { $0.id == id }
    }

    func allLoadedStaffReports() -> [StaffModerationReport] {
        let values = if mode == .grouped {
            clusters.flatMap(\.reports) + duplicateClusters.flatMap(\.clusters).flatMap(\.reports)
        } else {
            staffReports
        }
        var seen: Set<String> = []
        return values.filter { report in
            !evictedReportIds.contains(report.id) && seen.insert(report.id).inserted
        }
    }

    public func activeReports(in cluster: StaffModerationReportEntityCluster) -> [StaffModerationReport] {
        cluster.reports.filter(isActive)
    }

    public func activeReportCount(in cluster: StaffModerationReportEntityCluster) -> Int {
        activeReports(in: cluster).count
    }

    public func hasActiveReports(in duplicateCluster: StaffModerationReportDuplicateCluster) -> Bool {
        duplicateCluster.clusters.contains { !activeReports(in: $0).isEmpty }
    }

    public func presentationCounts(
        for duplicateCluster: StaffModerationReportDuplicateCluster
    ) -> (posts: Int, reports: Int) {
        let loaded = duplicateCluster.clusters.flatMap(\.reports)
        let activeClusters = duplicateCluster.clusters.filter { !activeReports(in: $0).isEmpty }
        let activeCount = activeClusters.reduce(0) { $0 + activeReports(in: $1).count }
        if loaded.count == activeCount {
            return (duplicateCluster.postCount, duplicateCluster.reportCount)
        }
        return (activeClusters.count, activeCount)
    }

    func beginQueueMutation() -> Bool {
        guard !isQueueActionInProgress else { return false }
        isMutatingQueue = true
        return true
    }

    func evict(_ reportIds: Set<String>) {
        evictedReportIds.formUnion(reportIds)
        staffReports.removeAll { reportIds.contains($0.id) }
        selectedReportIds.subtract(reportIds)
        actionErrors = actionErrors.filter { !reportIds.contains($0.key) }
    }

    func acceptAuthoritativeReports() {
        evictedReportIds.removeAll()
    }

    func resetReportQueueScope() {
        evictedReportIds.removeAll()
        groupedLoadedPageCount = 0
    }

    func cancelReportPaginationForRefresh() {
        reportPagination.reset(items: reportPagination.items)
        activeReportPageRequest = nil
    }

    private func isActive(_ report: StaffModerationReport) -> Bool {
        !evictedReportIds.contains(report.id)
    }

    func message(for error: Error) -> UiVerbatimText {
        switch error {
        case let error as VouchaError:
            .verbatim(String(describing: error))
        case AdminWarningValidationError.reasonTooLong:
            .message(.nativeSwiftModerationReportsWarningReasonTooLong)
        case AdminWarningValidationError.publicMessageTooLong:
            .message(.nativeSwiftModerationReportsWarningMessageTooLong)
        default:
            .message(.nativeSwiftModerationReportsActionFailed)
        }
    }
}
