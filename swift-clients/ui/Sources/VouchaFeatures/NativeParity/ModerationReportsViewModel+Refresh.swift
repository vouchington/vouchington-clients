import VouchaAPI
import VouchaModels

extension ModerationReportsViewModel {
    func refreshGroupedAfterActions() async {
        guard viewerTier.isStaff, mode == .grouped, !isRefreshingGrouped, let client else { return }
        let requestedPageCount = max(groupedLoadedPageCount, 1)
        generation += 1
        let requestGeneration = generation
        cancelReportPaginationForRefresh()
        isRefreshingGrouped = true
        paginationError = nil
        defer { isRefreshingGrouped = false }
        var refreshedClusters: [StaffModerationReportEntityCluster] = []
        var refreshedDuplicates: [StaffModerationReportDuplicateCluster] = []
        var cursor: String?
        var finalPageInfo: Page<StaffModerationReport>.PageInfo?
        var acceptedPageCount = 0
        do {
            for pageIndex in 0 ..< requestedPageCount {
                let response: StaffClusteredModerationReportsResponse = try await client.send(
                    .clusteredModerationReports(status: status, after: cursor)
                )
                try Task.checkCancellation()
                guard requestGeneration == generation else { return }
                refreshedClusters = deduplicated(refreshedClusters + response.clusters, id: \.id)
                refreshedDuplicates = mergedDuplicateClusters(refreshedDuplicates, response.duplicateClusters)
                finalPageInfo = response.pageInfo
                acceptedPageCount += 1
                guard pageIndex + 1 < requestedPageCount,
                      response.pageInfo.hasNextPage,
                      let nextCursor = response.pageInfo.endCursor
                else { break }
                cursor = nextCursor
            }
            guard requestGeneration == generation, let finalPageInfo else { return }
            clusters = refreshedClusters
            duplicateClusters = refreshedDuplicates
            staffReports = []
            groupedLoadedPageCount = acceptedPageCount
            applyPageInfo(finalPageInfo)
            acceptAuthoritativeReports()
            loadError = nil
        } catch is CancellationError {
            return
        } catch {
            guard requestGeneration == generation else { return }
            paginationError = message(for: error)
        }
    }

    func mergedDuplicateClusters(
        _ current: [StaffModerationReportDuplicateCluster],
        _ incoming: [StaffModerationReportDuplicateCluster]
    ) -> [StaffModerationReportDuplicateCluster] {
        var merged = current
        var indexes = Dictionary(uniqueKeysWithValues: current.enumerated().map { ($1.id, $0) })
        for duplicate in incoming {
            guard let index = indexes[duplicate.id] else {
                indexes[duplicate.id] = merged.count
                merged.append(duplicate)
                continue
            }
            merged[index] = mergeDuplicateCluster(merged[index], duplicate)
        }
        return merged
    }

    private func mergeDuplicateCluster(
        _ current: StaffModerationReportDuplicateCluster,
        _ incoming: StaffModerationReportDuplicateCluster
    ) -> StaffModerationReportDuplicateCluster {
        let clusters = deduplicated(current.clusters + incoming.clusters, id: \.id)
        var reasonOrder: [String] = []
        var reasonCounts: [String: Int] = [:]
        for item in clusters.flatMap(\.reasonBreakdown) {
            if reasonCounts[item.reason] == nil {
                reasonOrder.append(item.reason)
            }
            reasonCounts[item.reason, default: 0] += item.count
        }
        return StaffModerationReportDuplicateCluster(
            id: current.id,
            signal: current.signal,
            postCount: clusters.count,
            reportCount: clusters.reduce(0) { $0 + $1.reportCount },
            reasonBreakdown: reasonOrder.map {
                ModerationReportReasonBreakdown(reason: $0, count: reasonCounts[$0, default: 0])
            },
            firstReportedAt: clusters.map(\.firstReportedAt).min()
                ?? min(current.firstReportedAt, incoming.firstReportedAt),
            lastReportedAt: clusters.map(\.lastReportedAt).max()
                ?? max(current.lastReportedAt, incoming.lastReportedAt),
            clusters: clusters
        )
    }
}
