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
            confirm(response.flag)
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
                if !currentIds.subtracting(baselineIds).isEmpty {
                    confirmedPenaltyFlagIds.insert(flagId)
                }
                try await confirm(service.flag(id: flagId))
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
}
