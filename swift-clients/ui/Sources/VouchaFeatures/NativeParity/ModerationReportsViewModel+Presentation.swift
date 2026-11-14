import VouchaModels

public extension ModerationReportsViewModel {
    func presentationCounts(
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
}
