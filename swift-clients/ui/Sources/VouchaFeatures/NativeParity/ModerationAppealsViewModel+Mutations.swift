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
            let queued: ModerationAppealQueueResponse = try await client.send(
                .appealResolutionDrafts(id: appeal.id)
            )
            guard queued.queued else {
                throw VouchaError.unexpected("The AI draft was not queued.")
            }
            for attempt in 0 ..< rerunPollAttempts {
                let refreshed: ModerationAppealEnvelope = try await client.send(.appeal(id: appeal.id))
                if refreshed.appeal.aiDraftedAt != nil,
                   refreshed.appeal.aiDraftedAt != appeal.aiDraftedAt,
                   refreshed.appeal.latestLifecycleChangeId != appeal.latestLifecycleChangeId {
                    replace(with: refreshed.appeal)
                    return
                }
                if attempt + 1 < rerunPollAttempts {
                    try await rerunPollDelay()
                }
            }
            throw VouchaError.unexpected("The AI draft is still processing. Refresh to check again.")
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

    private func withMutation(
        _ appealId: String,
        marksAmbiguousDelivery: Bool = false,
        operation: () async throws -> Void
    ) async -> Bool {
        guard mutatingAppealId == nil else { return false }
        mutatingAppealId = appealId
        errorMessage = nil
        defer { mutatingAppealId = nil }
        do {
            try await operation()
            return true
        } catch {
            if marksAmbiguousDelivery, isAmbiguousDelivery(error) {
                ambiguousDeliveryAppealIds.insert(appealId)
            }
            errorMessage = message(for: error)
            return false
        }
    }

    private func isAmbiguousDelivery(_ error: Error) -> Bool {
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
