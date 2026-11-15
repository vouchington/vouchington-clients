import Foundation
import VouchaAPI
import VouchaLocalization
import VouchaModels

struct AppealAIRerunBaseline: Equatable {
    var runId: String?

    func isReconciled(in appeal: ModerationAppeal) -> Bool {
        (runId != nil && appeal.aiDraftRunId == runId)
            || appeal.approvedAt != nil || appeal.sentAt != nil || appeal.status != .pending
    }
}

extension ModerationAppealsViewModel {
    func canRerun(_ appeal: ModerationAppeal) -> Bool {
        admissionOwner != nil && canMutate(appeal) && appeal.approvedAt == nil && appeal.sentAt == nil
            && appeals.contains(where: { $0.id == appeal.id })
            && pendingAIReruns[appeal.id] == nil
    }

    func canRefreshAIRerun(_ appeal: ModerationAppeal) -> Bool {
        canAccess && !isMutating && appeal.status == .pending
            && appeals.contains(where: { $0.id == appeal.id })
            && pendingAIReruns[appeal.id] != nil
    }

    /// A pending admission owns the display until the row names its run, so a previous
    /// run's terminal status never shows beside a re-run that is still in flight.
    func aiDraftStatusKey(for appeal: ModerationAppeal) -> UiMessageKey? {
        if let baseline = pendingAIReruns[appeal.id] {
            let admitted = baseline.runId
            if admitted == nil || appeal.aiDraftRequestedRunId != admitted {
                return ModerationAIRunStatus.active.appealDraftKey
            }
        }
        return appeal.aiDraftRequestedRunStatus?.appealDraftKey
    }

    func startAIRerun(for appeal: ModerationAppeal) {
        guard canRerun(appeal) else { return }
        aiRerunTasks[appeal.id]?.cancel()
        let token = UUID()
        aiRerunTaskTokens[appeal.id] = token
        aiRerunTasks[appeal.id] = Task { [weak self] in
            guard let self else { return }
            await rerunAI(for: appeal)
            guard aiRerunTaskTokens[appeal.id] == token else { return }
            aiRerunTaskTokens.removeValue(forKey: appeal.id)
            aiRerunTasks.removeValue(forKey: appeal.id)
        }
    }

    func cancelAIReruns() {
        for task in aiRerunTasks.values {
            task.cancel()
        }
        aiRerunTaskTokens.removeAll()
        aiRerunTasks.removeAll()
    }

    func reconcileAIReruns(from appeals: [ModerationAppeal]) {
        admissionStore.reconcile(appeals, for: admissionOwner)
    }

    func refreshPendingAIRerun(for appeal: ModerationAppeal) async {
        guard canRefreshAIRerun(appeal), let client,
              let admission = admissionStore.admission(appeal.id, for: admissionOwner)
        else { return }
        _ = await withMutation(appeal.id) {
            try await reconcileAIRun(for: appeal, admission: admission, client: client)
        }
    }
}

enum AppealAIRerunError: Error, Equatable {
    case notQueued
    case stillProcessing
    case ended
}
