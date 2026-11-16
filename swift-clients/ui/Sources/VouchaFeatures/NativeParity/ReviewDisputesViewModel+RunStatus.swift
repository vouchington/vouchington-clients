import VouchaCore
import VouchaLocalization
import VouchaModels

extension ReviewDisputesViewModel {
    /// A pending admission owns the display until the row names its run, so a previous
    /// run's terminal status never shows beside a re-run that is still in flight.
    func aiDraftStatusKey(for dispute: ReviewDispute) -> UiMessageKey? {
        if let baseline = ambiguousAIRerunBaselines[dispute.id] {
            let admitted = baseline.runId
            if admitted == nil || dispute.aiDraftRequestedRunId != admitted {
                return ModerationAIRunStatus.active.disputeDraftKey
            }
        }
        return dispute.aiDraftRequestedRunStatus?.disputeDraftKey
    }

    /// The row names the status of the exact admitted run. Show it, and end the admission
    /// only when that same run failed or expired; never re-POST to find out.
    func applyRequestedRunStatus(of dispute: ReviewDispute, baseline: AIRerunBaseline) throws {
        guard let runId = baseline.runId, dispute.aiDraftRequestedRunId == runId,
              let status = dispute.aiDraftRequestedRunStatus
        else { return }
        replaceWithTargetedOutcome(dispute, preservingUnconfirmedPublicDraft: true)
        let ended: UiMessageKey
        switch status {
        case .failed: ended = .nativeSwiftReviewDisputesRerunFailed
        case .expired: ended = .nativeSwiftReviewDisputesRerunExpired
        case .active, .succeeded: return
        }
        clearAmbiguity(for: dispute.id)
        throw VouchaError.unexpected(UiMessages.string(ended, locale: .english))
    }
}

extension ModerationAIRunStatus {
    /// Dispute drafts reuse the shared dispute copy; the producer ships no per-status dispute keys.
    var disputeDraftKey: UiMessageKey {
        switch self {
        case .active: .nativeSwiftReviewDisputesRerunStillProcessing
        case .succeeded: .nativeSwiftReviewDisputesDrafted
        case .failed: .nativeSwiftReviewDisputesRerunFailed
        case .expired: .nativeSwiftReviewDisputesRerunExpired
        }
    }
}
