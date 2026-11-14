import Foundation
import VouchaAPI
import VouchaCore
import VouchaModels

extension ModerationAppealsViewModel {
    @discardableResult
    func saveDraft(for appeal: ModerationAppeal) async -> Bool {
        guard canEdit(appeal), let client else { return false }
        let draft = drafts[appeal.id] ?? appeal.publicResponse ?? ""
        guard draft != (appeal.publicResponse ?? "") else {
            setDraft(draft, for: appeal)
            return true
        }
        return await withMutation(appeal.id) {
            let response: ModerationAppealEnvelope = try await client.send(
                .updateAppeal(id: appeal.id, publicResponse: draft)
            )
            replace(with: response.appeal)
        }
    }

    func approve(_ appeal: ModerationAppeal) async {
        guard canApprove(appeal), let client else { return }
        let draft = drafts[appeal.id] ?? appeal.publicResponse ?? ""
        _ = await withMutation(appeal.id) {
            if draft != (appeal.publicResponse ?? "") {
                let saved: ModerationAppealEnvelope = try await client.send(
                    .updateAppeal(id: appeal.id, publicResponse: draft)
                )
                replace(with: saved.appeal)
            }
            let approved: ModerationAppealEnvelope = try await client.send(.appealApproval(id: appeal.id))
            replace(with: approved.appeal)
        }
    }

    func send(_ appeal: ModerationAppeal) async {
        guard canSend(appeal), let client else { return }
        _ = await withMutation(appeal.id, marksAmbiguousDelivery: true) {
            let response: ModerationAppealEnvelope = try await client.send(.appealDelivery(id: appeal.id))
            ambiguousDeliveryAppealIds.remove(appeal.id)
            replace(with: response.appeal)
        }
    }

    func resolve(_ appeal: ModerationAppeal, action: ModerationAppealAction) async {
        guard canResolve(appeal, action: action), let client else { return }
        _ = await withMutation(appeal.id) {
            let response: ModerationAppealEnvelope = try await client.send(
                .appealResolution(id: appeal.id, action: action)
            )
            replace(with: response.appeal)
        }
    }

    func rerunAI(for appeal: ModerationAppeal) async {
        guard canRerun(appeal), let client else { return }
        _ = await withMutation(appeal.id) {
            try Task.checkCancellation()
            guard let admission = admissionStore.admit(appeal, for: admissionOwner) else { return }
            let queued: ModerationAppealQueueResponse
            do {
                queued = try await client.send(.appealResolutionDrafts(id: appeal.id))
            } catch {
                if !isAmbiguousDelivery(error) {
                    admissionStore.remove(appeal.id, for: admissionOwner, matching: admission.token)
                }
                throw error
            }
            guard queued.queued else {
                admissionStore.remove(appeal.id, for: admissionOwner, matching: admission.token)
                throw AppealAIRerunError.notQueued
            }
            guard let admitted = admissionStore.recordRun(
                queued.runId, appealId: appeal.id, owner: admissionOwner, token: admission.token
            ) else { throw AppealAIRerunError.stillProcessing }
            try await reconcileAIRun(for: appeal, admission: admitted, client: client)
        }
    }

    func clearAmbiguousDelivery(for appealId: String) async {
        guard ambiguousDeliveryAppealIds.contains(appealId), canAccess, let client else { return }
        _ = await withMutation(appealId) {
            let response: ModerationAppealEnvelope = try await client.send(.appeal(id: appealId))
            replace(with: response.appeal)
            ambiguousDeliveryAppealIds.remove(appealId)
        }
    }

    func withMutation(
        _ appealId: String,
        marksAmbiguousDelivery: Bool = false,
        operation: () async throws -> Void
    ) async -> Bool {
        guard mutatingAppealId == nil else { return false }
        mutationAdmissionToken = admissionStore.admission(appealId, for: admissionOwner)?.token
        mutatingAppealId = appealId
        errorMessage = nil
        defer {
            mutationAdmissionToken = nil
            mutatingAppealId = nil
        }
        do {
            try await operation()
            return true
        } catch {
            if marksAmbiguousDelivery, isAmbiguousDelivery(error) {
                ambiguousDeliveryAppealIds.insert(appealId)
            }
            if let rerunError = error as? AppealAIRerunError {
                switch rerunError {
                case .notQueued: errorMessage = .message(.nativeSwiftReviewDisputesRerunNotQueued)
                case .stillProcessing: errorMessage = .message(.nativeSwiftReviewDisputesRerunStillProcessing)
                case .ended: errorMessage = .message(.nativeSwiftModerationAppealsGenericError)
                }
            } else {
                errorMessage = message(for: error)
            }
            return false
        }
    }

    private func isAmbiguousDelivery(_ error: Error) -> Bool {
        if error is CancellationError {
            return true
        }
        if error is URLError {
            return true
        }
        guard let error = error as? VouchaError else { return false }
        switch error {
        case .network, .cancelled, .decodingFailed:
            return true
        case let .api(statusCode, _), let .apiMessage(statusCode, _, _):
            return statusCode == 0 || statusCode == 408 || statusCode >= 500
        default:
            return false
        }
    }
}
