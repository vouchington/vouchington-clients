extension IntegrityPenaltyLedgerViewModel {
    func revoke(_ penalty: IntegrityPenaltyRow) async {
        guard canRevoke(penalty), let service else { return }
        guard mutatingPenaltyIds.insert(penalty.id).inserted else { return }
        mutationErrors[penalty.id] = nil
        do {
            try await confirm(service.revoke(domain: domain, id: penalty.id))
            mutatingPenaltyIds.remove(penalty.id)
        } catch {
            guard integrityMutationIsAmbiguous(error) else {
                mutationErrors[penalty.id] = .revokeFailed
                mutatingPenaltyIds.remove(penalty.id)
                return
            }
            reconciliationRequiredIds.insert(penalty.id)
            mutationErrors[penalty.id] = .resultUncertain
            mutatingPenaltyIds.remove(penalty.id)
            await reconcile(penalty.id)
        }
    }

    func reconcile(_ penaltyId: String) async {
        guard reconciliationRequiredIds.contains(penaltyId),
              reconcilingPenaltyIds.insert(penaltyId).inserted,
              let service else { return }
        defer { reconcilingPenaltyIds.remove(penaltyId) }
        do {
            try await confirm(service.penalty(domain: domain, id: penaltyId))
            reconciliationRequiredIds.remove(penaltyId)
            mutationErrors[penaltyId] = nil
        } catch {
            mutationErrors[penaltyId] = .reconciliationFailed
        }
    }

    func canRevoke(_ penalty: IntegrityPenaltyRow) -> Bool {
        guard let current = penalties.first(where: { $0.id == penalty.id }) else { return false }
        return canAccess && viewerTier.canRevokeIntegrityPenalty
            && selectedStatus != .revoked && current.revokedAt == nil
            && !mutatingPenaltyIds.contains(penalty.id)
            && !reconcilingPenaltyIds.contains(penalty.id)
            && !reconciliationRequiredIds.contains(penalty.id)
    }

    private func confirm(_ penalty: IntegrityPenaltyRow) {
        loadGeneration += 1
        isLoading = false
        isLoadingMore = false
        let matches = switch selectedStatus {
        case .active: penalty.revokedAt == nil
        case .revoked: penalty.revokedAt != nil
        case .all: true
        }
        guard matches else {
            penalties.removeAll { $0.id == penalty.id }
            return
        }
        if let index = penalties.firstIndex(where: { $0.id == penalty.id }) {
            penalties[index] = penalty
        } else {
            penalties.insert(penalty, at: 0)
        }
    }

}
