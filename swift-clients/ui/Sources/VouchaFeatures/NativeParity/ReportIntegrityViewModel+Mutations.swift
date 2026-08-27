import VouchaModels

extension ReportIntegrityViewModel {
    func dismiss(_ flag: ReportIntegrityFlag) async {
        guard canResolve(flag), let service else { return }
        await withMutation(flag.id) { try await confirm(service.dismiss(flagId: flag.id)) }
    }

    func penalizeReporters(_ flag: ReportIntegrityFlag) async {
        guard canApplyPenalty(flag), let service else { return }
        await withMutation(flag.id) {
            let response = try await service.penalizeReporters(flagId: flag.id)
            penalizedReporterCounts[flag.id] = response.penalizedUserCount
            confirm(response.flag)
        }
    }

    func canResolve(_ flag: ReportIntegrityFlag) -> Bool {
        viewerTier.canResolveIntegrityFlag && canOperate(flag)
    }

    func canApplyPenalty(_ flag: ReportIntegrityFlag) -> Bool {
        viewerTier.canApplyIntegrityPenalty && canOperate(flag)
    }

    func reconcile(_ flagId: String) async {
        guard reconciliationRequiredFlagIds.contains(flagId),
              reconcilingFlagIds.insert(flagId).inserted,
              let service else { return }
        defer { reconcilingFlagIds.remove(flagId) }
        do {
            try await confirm(service.flag(id: flagId))
            reconciliationRequiredFlagIds.remove(flagId)
            mutationErrorMessages[flagId] = nil
        } catch {
            mutationErrorMessages[flagId] = integrityErrorMessage(error)
        }
    }

    private func confirm(_ flag: ReportIntegrityFlag) {
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

    private func canOperate(_ flag: ReportIntegrityFlag) -> Bool {
        canAccess && flag.resolvedAt == nil && flags.contains { $0.id == flag.id }
            && !mutatingFlagIds.contains(flag.id) && !reconcilingFlagIds.contains(flag.id)
            && !reconciliationRequiredFlagIds.contains(flag.id)
    }
}
