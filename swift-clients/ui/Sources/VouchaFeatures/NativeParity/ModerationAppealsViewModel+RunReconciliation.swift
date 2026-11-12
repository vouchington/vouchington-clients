import Foundation
import VouchaAPI
import VouchaModels

extension ModerationAppealsViewModel {
    func reconcileAIRun(
        for appeal: ModerationAppeal,
        admission: AppealAIRerunAdmissionStore.Admission,
        client: APIClient
    ) async throws {
        let revision = loadRevision
        func isCurrent() -> Bool {
            !Task.isCancelled && revision == loadRevision && selectedStatus == .pending
                && appeals.contains(where: { $0.id == appeal.id })
                && admissionStore.admission(appeal.id, for: admissionOwner)?.token == admission.token
        }
        // An ambiguous POST has no safe run identity. Keep its admission until a known
        // terminal item mutation; a newer requested run cannot establish ownership.
        guard let runId = admission.baseline.runId else {
            try await reconcileAIRunDetail(
                for: appeal, admission: admission, client: client, isCurrent: isCurrent
            )
            return
        }
        for attempt in 0 ..< rerunPollAttempts {
            guard isCurrent() else { return }
            let run: ModerationAppealDraftRunResponse = try await client.send(
                .appealResolutionDraftRun(id: appeal.id, runId: runId)
            )
            guard isCurrent() else { return }
            guard run.runId == runId else { throw AppealAIRerunError.stillProcessing }
            switch run.status {
            case .failed, .expired:
                admissionStore.remove(appeal.id, for: admissionOwner, matching: admission.token)
                throw AppealAIRerunError.ended
            case .succeeded:
                try await reconcileAIRunDetail(
                    for: appeal, admission: admission, client: client, isCurrent: isCurrent
                )
                return
            case .active:
                if attempt + 1 < rerunPollAttempts { try await rerunPollDelay() }
            }
        }
        throw AppealAIRerunError.stillProcessing
    }

    private func reconcileAIRunDetail(
        for appeal: ModerationAppeal,
        admission: AppealAIRerunAdmissionStore.Admission,
        client: APIClient,
        isCurrent: () -> Bool
    ) async throws {
        let response: ModerationAppealEnvelope = try await client.send(.appeal(id: appeal.id))
        guard isCurrent(), response.appeal.id == appeal.id else { return }
        guard admission.baseline.isReconciled(in: response.appeal) else {
            throw AppealAIRerunError.stillProcessing
        }
        admissionStore.remove(appeal.id, for: admissionOwner, matching: admission.token)
        replace(with: response.appeal, preservingEditedDraft: true)
    }

}
