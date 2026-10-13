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
}
