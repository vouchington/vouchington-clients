import VouchaAPI
import VouchaLocalization
import VouchaModels

public extension ModerationReportsViewModel {
    func bulkDismissSelected() async {
        guard viewerTier.isStaff, let client, beginQueueMutation() else { return }
        defer { isMutatingQueue = false }
        let selected = allLoadedStaffReports().filter { selectedReportIds.contains($0.id) }
        let eligible = selected.filter { actionIsAllowed(.dismiss, for: $0) }
        let skipped = Set(selected.map(\.id)).subtracting(eligible.map(\.id))
        skipped.forEach { actionErrors[$0] = .message(.nativeSwiftModerationReportsSkippedResolved) }
        await resolveAllSettled(eligible, as: .dismissed, client: client)
        if mode == .grouped {
            await refreshGroupedAfterActions()
        }
    }

    func bulkRemoveSelected() async {
        guard viewerTier.canRemove, let client, beginQueueMutation() else { return }
        defer { isMutatingQueue = false }
        let loadedReports = allLoadedStaffReports()
        let selected = loadedReports.filter { selectedReportIds.contains($0.id) }
        let grouped = Dictionary(grouping: selected.filter { actionIsAllowed(.removeTarget, for: $0) }) {
            "\($0.entityType):\($0.entityId)"
        }
        let eligibleIds = Set(grouped.values.flatMap { $0 }.map(\.id))
        for item in Set(selected.map(\.id)).subtracting(eligibleIds) {
            actionErrors[item] = .message(.nativeSwiftModerationReportsOrdinaryReportsOnly)
        }
        for reports in grouped.values {
            guard let representative = reports.first else { continue }
            let ids = Set(reports.map(\.id))
            inFlightReportIds.formUnion(ids)
            do {
                let _: EmptyResponse = try await client.send(.deletePost(postId: representative.entityId))
                let allTargetIds = Set(loadedReports.filter {
                    $0.entityType == representative.entityType && $0.entityId == representative.entityId
                }.map(\.id))
                evict(allTargetIds)
            } catch {
                ids.forEach { actionErrors[$0] = message(for: error) }
            }
            inFlightReportIds.subtract(ids)
        }
        if mode == .grouped {
            await refreshGroupedAfterActions()
        }
    }

    func dismissLoadedReports(in clusterId: String) async {
        guard viewerTier.isStaff, let client,
              let cluster = clusters.first(where: { $0.id == clusterId }),
              beginQueueMutation()
        else { return }
        defer { isMutatingQueue = false }
        let loaded = activeReports(in: cluster)
        let eligible = loaded.filter { actionIsAllowed(.dismiss, for: $0) }
        let skipped = Set(loaded.map(\.id)).subtracting(eligible.map(\.id))
        skipped.forEach { actionErrors[$0] = .message(.nativeSwiftModerationReportsDedicatedDecision) }
        await resolveAllSettled(eligible, as: .dismissed, client: client)
        await refreshGroupedAfterActions()
    }

    private func resolveAllSettled(
        _ reports: [StaffModerationReport],
        as status: ModerationReportResolution,
        client: APIClient
    ) async {
        for report in reports {
            guard !inFlightReportIds.contains(report.id) else { continue }
            inFlightReportIds.insert(report.id)
            actionErrors[report.id] = nil
            do {
                try await resolve(report, as: status, client: client)
            } catch {
                actionErrors[report.id] = message(for: error)
            }
            inFlightReportIds.remove(report.id)
        }
    }
}
