import Foundation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

extension ReviewDisputesViewModel {
    @discardableResult
    func savePublicResponse(for dispute: ReviewDispute) async -> Bool {
        guard canEdit(dispute), let client else { return false }
        let draft = publicDraft(for: dispute)
        guard draft != (dispute.publicResponse ?? "") else {
            setPublicResponseDraft(draft, for: dispute)
            return true
        }
        return await withMutation(dispute.id) {
            let response: ReviewDisputeResponse = try await client.send(
                .updateDisputeDraft(id: dispute.id, publicResponse: draft)
            )
            replaceWithTargetedOutcome(response.dispute)
        }
    }

    func approve(_ dispute: ReviewDispute) async {
        guard canApprove(dispute), let client else { return }
        let draft = publicDraft(for: dispute)
        _ = await withMutation(dispute.id, marksAmbiguous: true) {
            if draft != (dispute.publicResponse ?? "") {
                let saved: ReviewDisputeResponse = try await client.send(
                    .updateDisputeDraft(id: dispute.id, publicResponse: draft)
                )
                replaceWithTargetedOutcome(saved.dispute)
            }
            let approved: ReviewDisputeResponse = try await client.send(.disputeApproval(id: dispute.id))
            ambiguousDisputeIds.remove(dispute.id)
            replaceWithTargetedOutcome(approved.dispute)
        }
    }

    func deliver(_ dispute: ReviewDispute) async {
        guard canDeliver(dispute), let client else { return }
        _ = await withMutation(dispute.id, marksAmbiguous: true) {
            let response: ReviewDisputeResponse = try await client.send(.disputeDelivery(id: dispute.id))
            ambiguousDisputeIds.remove(dispute.id)
            replaceWithTargetedOutcome(response.dispute)
        }
    }

    func resolve(_ dispute: ReviewDispute, action: ReviewDisputeResolutionAction) async {
        guard canResolve(dispute, action: action), let client else { return }
        let body: String?
        if action == .annotate {
            guard let annotationBody = annotationBody(for: dispute) else { return }
            body = annotationBody
        } else {
            body = nil
        }
        _ = await withMutation(dispute.id, marksAmbiguous: true) {
            let response: ReviewDisputeResponse = try await client.send(
                .disputeResolution(id: dispute.id, action: action, bodyText: body)
            )
            replaceWithTargetedOutcome(response.dispute)
        }
    }

    func rerunAI(for dispute: ReviewDispute) async {
        guard canRerun(dispute), let client else { return }
        let baseline = AIRerunBaseline(dispute: dispute)
        let completed = await withMutation(dispute.id) {
            let queued: ReviewDisputeQueueResponse = try await client.send(
                .disputeResolutionDrafts(id: dispute.id)
            )
            guard queued.queued else {
                throw VouchaError.unexpected(
                    UiMessages.string(.nativeSwiftReviewDisputesRerunNotQueued, locale: .english)
                )
            }
            ambiguousDisputeIds.insert(dispute.id)
            ambiguousAIRerunBaselines[dispute.id] = baseline
            for attempt in 0 ..< rerunPollAttempts {
                let refreshed: ReviewDisputeResponse = try await client.send(.dispute(id: dispute.id))
                if baseline.isReconciled(in: refreshed.dispute) {
                    replaceWithTargetedOutcome(
                        refreshed.dispute,
                        preservingUnconfirmedPublicDraft: true
                    )
                    clearAmbiguity(for: dispute.id)
                    return
                }
                if attempt + 1 < rerunPollAttempts {
                    try await rerunPollDelay()
                }
            }
            throw VouchaError.unexpected(
                UiMessages.string(.nativeSwiftReviewDisputesRerunStillProcessing, locale: .english)
            )
        }
        if !completed, ambiguousDisputeIds.contains(dispute.id) {
            ambiguousAIRerunBaselines[dispute.id] = baseline
        }
    }

    func refreshAmbiguousMutation(for disputeId: String) async {
        guard ambiguousDisputeIds.contains(disputeId), canAccess, let client else { return }
        _ = await withMutation(disputeId) {
            let response: ReviewDisputeResponse = try await client.send(.dispute(id: disputeId))
            replaceWithTargetedOutcome(
                response.dispute,
                preservingUnconfirmedPublicDraft: true
            )
            if let baseline = ambiguousAIRerunBaselines[disputeId],
               !baseline.isReconciled(in: response.dispute) {
                throw VouchaError.unexpected(
                    UiMessages.string(.nativeSwiftReviewDisputesRerunStillProcessing, locale: .english)
                )
            }
            clearAmbiguity(for: disputeId)
        }
    }

    private func clearAmbiguity(for disputeId: String) {
        ambiguousDisputeIds.remove(disputeId)
        ambiguousAIRerunBaselines.removeValue(forKey: disputeId)
    }

    private func withMutation(
        _ disputeId: String,
        marksAmbiguous: Bool = false,
        operation: () async throws -> Void
    ) async -> Bool {
        guard mutatingDisputeId == nil else { return false }
        mutatingDisputeId = disputeId
        errorMessage = nil
        defer { mutatingDisputeId = nil }
        do {
            try await operation()
            return true
        } catch {
            if marksAmbiguous, isAmbiguous(error) {
                ambiguousDisputeIds.insert(disputeId)
            }
            errorMessage = message(for: error)
            return false
        }
    }

    private func isAmbiguous(_ error: Error) -> Bool {
        if error is CancellationError {
            return true
        }
        if error is URLError {
            return true
        }
        guard let error = error as? VouchaError else { return false }
        switch error {
        case .network, .cancelled:
            return true
        case let .api(statusCode, _), let .apiMessage(statusCode, _, _):
            return statusCode == 0 || statusCode >= 500
        default:
            return false
        }
    }
}
