import Foundation
import VouchaModels

extension RewardsProgramStatusesViewModel {
    func reconciled(_ serverValues: [RewardsProgramStatus], protecting readId: UUID? = nil) -> [RewardsProgramStatus] {
        var values = Dictionary(serverValues.map { ($0.id, $0) }, uniquingKeysWith: { _, latest in latest })
        if let readId {
            for local in localUpserts.values where local.pendingReadIds.contains(readId) {
                values[local.value.id] = local.value
            }
            for (id, deletion) in pendingDeletions where deletion.pendingReadIds.contains(readId) {
                values.removeValue(forKey: id)
            }
        } else {
            for local in localUpserts.values {
                values[local.value.id] = local.value
            }
            for id in pendingDeletions.keys {
                values.removeValue(forKey: id)
            }
        }
        return values.values.sorted { $0.id < $1.id }
    }

    func upsert(_ status: RewardsProgramStatus) {
        trackLocalUpsert(status)
        pendingDeletions.removeValue(forKey: status.id)
        statuses = statuses.filter { $0.id != status.id } + [status]
        mutationErrorMessage = nil
    }

    func beginRead() -> UUID {
        let readId = UUID()
        inFlightReadIds.insert(readId)
        for deletion in pendingDeletions.values where deletion.mutationPending {
            deletion.pendingReadIds.insert(readId)
        }
        return readId
    }

    func completeRead(_ readId: UUID) {
        inFlightReadIds.remove(readId)
        for id in Array(localUpserts.keys) where localUpserts[id]?.pendingReadIds.remove(readId) != nil {
            if localUpserts[id]?.pendingReadIds.isEmpty == true {
                localUpserts.removeValue(forKey: id)
            }
        }
        for id in Array(pendingDeletions.keys) where pendingDeletions[id]?.pendingReadIds.remove(readId) != nil {
            if pendingDeletions[id]?.mutationPending == false, pendingDeletions[id]?.pendingReadIds.isEmpty == true {
                pendingDeletions.removeValue(forKey: id)
            }
        }
    }

    func completeLocalDeletion(_ id: String) {
        guard let deletion = pendingDeletions[id] else { return }
        deletion.mutationPending = false
        if deletion.pendingReadIds.isEmpty {
            pendingDeletions.removeValue(forKey: id)
        }
    }

    private func trackLocalUpsert(_ status: RewardsProgramStatus) {
        guard !inFlightReadIds.isEmpty else {
            localUpserts.removeValue(forKey: status.id)
            return
        }
        localUpserts[status.id] = .init(value: status, pendingReadIds: inFlightReadIds)
    }

    final class PendingLocalUpsert {
        let value: RewardsProgramStatus
        var pendingReadIds: Set<UUID>

        init(value: RewardsProgramStatus, pendingReadIds: Set<UUID>) {
            self.value = value
            self.pendingReadIds = pendingReadIds
        }
    }

    final class PendingLocalDeletion {
        var pendingReadIds: Set<UUID>
        var mutationPending = true

        init(pendingReadIds: Set<UUID>) {
            self.pendingReadIds = pendingReadIds
        }
    }
}
