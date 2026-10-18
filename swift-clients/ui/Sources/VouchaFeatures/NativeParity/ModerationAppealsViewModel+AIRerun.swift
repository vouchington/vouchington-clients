import Foundation
import VouchaAPI
import VouchaModels

struct AppealAIRerunBaseline: Equatable {
    let aiDraftedAt: Date?
    let lifecycleChangeId: String?

    init(appeal: ModerationAppeal) {
        aiDraftedAt = appeal.aiDraftedAt
        lifecycleChangeId = appeal.latestLifecycleChangeId
    }

    func isReconciled(in appeal: ModerationAppeal) -> Bool {
        (appeal.aiDraftedAt != nil
            && appeal.aiDraftedAt != aiDraftedAt
            && appeal.latestLifecycleChangeId != lifecycleChangeId)
            || appeal.approvedAt != nil
            || appeal.sentAt != nil
            || appeal.status != .pending
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
        let revision = loadRevision
        _ = await withMutation(appeal.id) {
            let response: ModerationAppealEnvelope = try await client.send(.appeal(id: appeal.id))
            guard revision == loadRevision, selectedStatus == .pending,
                  response.appeal.id == appeal.id,
                  appeals.contains(where: { $0.id == appeal.id }),
                  admissionStore.admission(appeal.id, for: admissionOwner) == admission
            else { return }
            guard admission.baseline.isReconciled(in: response.appeal) else {
                throw AppealAIRerunError.stillProcessing
            }
            admissionStore.remove(appeal.id, for: admissionOwner, matching: admission.token)
            replace(with: response.appeal, preservingEditedDraft: true)
        }
    }
}

enum AppealAIRerunError: Error, Equatable {
    case notQueued
    case stillProcessing
}
