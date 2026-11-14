extension IntegrityPenaltyReviewViewModel {
    func confirmReview(_ penalty: IntegrityPenaltyRow) async {
        guard canConfirm(penalty), let service else { return }
        guard mutatingIds.insert(penalty.id).inserted else { return }
        errors[penalty.id] = nil
        defer { mutatingIds.remove(penalty.id) }
        do {
            try await adopt(service.review(domain: domain, id: penalty.id))
        } catch {
            guard integrityMutationIsAmbiguous(error) else {
                errors[penalty.id] = failure(error, fallback: .reviewFailed)
                return
            }
            reconciliationRequiredIds.insert(penalty.id)
            errors[penalty.id] = .resultUncertain
            await reconcileWhileMutating(penalty.id)
        }
    }

    func revoke(_ penalty: IntegrityPenaltyRow) async {
        guard canRevoke(penalty), let service else { return }
        guard mutatingIds.insert(penalty.id).inserted else { return }
        errors[penalty.id] = nil
        defer { mutatingIds.remove(penalty.id) }
        do {
            _ = try await service.revoke(domain: domain, id: penalty.id)
            remove(penalty.id)
        } catch {
            guard integrityMutationIsAmbiguous(error) else {
                errors[penalty.id] = failure(error, fallback: .revokeFailed)
                return
            }
            reconciliationRequiredIds.insert(penalty.id)
            errors[penalty.id] = .resultUncertain
            await reconcileWhileMutating(penalty.id)
        }
    }

    func reconcile(_ penaltyId: String) async {
        guard reconciliationRequiredIds.contains(penaltyId),
              mutatingIds.insert(penaltyId).inserted else { return }
        defer { mutatingIds.remove(penaltyId) }
        await reconcileWhileMutating(penaltyId)
    }

    /// Reads the authoritative penalty. A review counts as committed only when the server's
    /// review date moved; otherwise the one review may be retried without advancing it twice.
    private func reconcileWhileMutating(_ penaltyId: String) async {
        guard let service else { return }
        let before = penalties.first { $0.id == penaltyId }
        do {
            let current = try await service.penalty(domain: domain, id: penaltyId)
            reconciliationRequiredIds.remove(penaltyId)
            errors[penaltyId] = nil
            if current.revokedAt != nil {
                remove(penaltyId)
            } else if current.reviewDueAt != before?.reviewDueAt || current.reviewedAt != before?.reviewedAt {
                adopt(current)
            }
        } catch {
            errors[penaltyId] = failure(error, fallback: .reconciliationFailed)
        }
    }

    private func adopt(_ penalty: IntegrityPenaltyRow) {
        if penalty.revokedAt != nil {
            remove(penalty.id)
            return
        }
        if let index = penalties.firstIndex(where: { $0.id == penalty.id }) {
            penalties[index] = penalty
        }
        committedIds.insert(penalty.id)
    }

    private func remove(_ penaltyId: String) {
        penalties.removeAll { $0.id == penaltyId }
        committedIds.remove(penaltyId)
    }
}
