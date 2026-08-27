import VouchaModels

extension VoteIntegrityViewModel {
    func resolve(_ flag: VoteIntegrityFlag, as resolution: VoteIntegrityResolution) async {
        guard canResolve(flag), let service else { return }
        await withMutation(flag.id) {
            try await confirm(service.resolve(flagId: flag.id, resolution: resolution))
        }
    }

    func applyPenalty(_ flag: VoteIntegrityFlag) async {
        guard canApplyPenalty(flag), let service else { return }
        guard mutatingFlagIds.insert(flag.id).inserted else { return }
        mutationErrorMessages[flag.id] = nil
        defer { mutatingFlagIds.remove(flag.id) }
        do {
            penaltyBaselineIdsByFlagId[flag.id] = try await service.penaltyIds(flagId: flag.id)
            let response = try await service.applyPenalty(flagId: flag.id)
            penalizedUserCounts[flag.id] = response.penalizedUserCount
            confirmedPenaltyFlagIds.insert(flag.id)
            penaltyBaselineIdsByFlagId[flag.id] = nil
        } catch {
            guard integrityMutationIsAmbiguous(error) else {
                penaltyBaselineIdsByFlagId[flag.id] = nil
                mutationErrorMessages[flag.id] = integrityErrorMessage(error)
                return
            }
            guard penaltyBaselineIdsByFlagId[flag.id] != nil else {
                mutationErrorMessages[flag.id] = integrityErrorMessage(error)
                return
            }
            ambiguousPenaltyFlagIds.insert(flag.id)
            reconciliationRequiredFlagIds.insert(flag.id)
            mutationErrorMessages[flag.id] = integrityErrorMessage(error)
            mutatingFlagIds.remove(flag.id)
            await reconcile(flag.id)
        }
    }

    func canResolve(_ flag: VoteIntegrityFlag) -> Bool {
        viewerTier.canResolveIntegrityFlag && canOperate(flag)
    }

    func canApplyPenalty(_ flag: VoteIntegrityFlag) -> Bool {
        viewerTier.canApplyIntegrityPenalty && canOperate(flag)
            && !confirmedPenaltyFlagIds.contains(flag.id)
            && !ambiguousPenaltyFlagIds.contains(flag.id)
    }

    func reconcile(_ flagId: String) async {
        guard reconciliationRequiredFlagIds.contains(flagId),
              reconcilingFlagIds.insert(flagId).inserted,
              let service else { return }
        defer { reconcilingFlagIds.remove(flagId) }
        do {
            if ambiguousPenaltyFlagIds.contains(flagId) {
                guard let baselineIds = penaltyBaselineIdsByFlagId[flagId] else {
                    throw IntegrityReviewServiceError.incompleteVotePenaltySnapshot
                }
                let currentIds = try await service.penaltyIds(flagId: flagId)
                if currentIds.subtracting(baselineIds).isEmpty {
                    try await confirm(service.flag(id: flagId))
                } else {
                    confirmedPenaltyFlagIds.insert(flagId)
                }
                penaltyBaselineIdsByFlagId[flagId] = nil
                ambiguousPenaltyFlagIds.remove(flagId)
                reconciliationRequiredFlagIds.remove(flagId)
                mutationErrorMessages[flagId] = nil
                return
            }
            try await confirm(service.flag(id: flagId))
            reconciliationRequiredFlagIds.remove(flagId)
            mutationErrorMessages[flagId] = nil
        } catch {
            mutationErrorMessages[flagId] = integrityErrorMessage(error)
        }
    }

    private func confirm(_ flag: VoteIntegrityFlag) {
        loadGeneration += 1
        isLoading = false
        isLoadingMore = false
        let matches = selectedStatus == .all
            || (selectedStatus == .pending) == (flag.resolvedAt == nil)
        guard matches else {
            flags.removeAll { $0.id == flag.id }
            return
        }
        if let index = flags.firstIndex(where: { $0.id == flag.id }) {
            flags[index] = flag
        } else {
            flags.insert(flag, at: 0)
        }
    }

    private func withMutation(_ flagId: String, operation: () async throws -> Void) async {
        guard mutatingFlagIds.insert(flagId).inserted else { return }
        mutationErrorMessages[flagId] = nil
        defer { mutatingFlagIds.remove(flagId) }
        do {
            try await operation()
        } catch {
            guard integrityMutationIsAmbiguous(error) else {
                mutationErrorMessages[flagId] = integrityErrorMessage(error)
                return
            }
            reconciliationRequiredFlagIds.insert(flagId)
            mutationErrorMessages[flagId] = integrityErrorMessage(error)
            mutatingFlagIds.remove(flagId)
            await reconcile(flagId)
        }
    }

    private func canOperate(_ flag: VoteIntegrityFlag) -> Bool {
        canAccess && flag.resolvedAt == nil && flags.contains { $0.id == flag.id }
            && !mutatingFlagIds.contains(flag.id) && !reconcilingFlagIds.contains(flag.id)
            && !reconciliationRequiredFlagIds.contains(flag.id)
    }
}
