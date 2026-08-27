import VouchaCore
import VouchaLocalization
import VouchaModels

extension ReviewDisputesViewModel {
    func replace(with dispute: ReviewDispute) {
        publicResponseDrafts[dispute.id] = publicDraftSeed(for: dispute)
        locallyEditedPublicResponseIds.remove(dispute.id)
        if annotationDrafts[dispute.id] == nil {
            annotationDrafts[dispute.id] = ""
        }
        guard dispute.status == selectedStatus else {
            disputes.removeAll { $0.id == dispute.id }
            return
        }
        if let index = disputes.firstIndex(where: { $0.id == dispute.id }) {
            disputes[index] = dispute
        } else {
            disputes.insert(dispute, at: 0)
        }
    }

    func replaceWithTargetedOutcome(
        _ dispute: ReviewDispute,
        preservingUnconfirmedPublicDraft: Bool = false
    ) {
        targetedOutcomeRevision += 1
        let localDraft = publicResponseDrafts[dispute.id]
        let shouldPreserveDraft = preservingUnconfirmedPublicDraft
            && locallyEditedPublicResponseIds.contains(dispute.id)
            && dispute.publicResponse != localDraft
        replace(with: dispute)
        if shouldPreserveDraft, let localDraft {
            publicResponseDrafts[dispute.id] = localDraft
            locallyEditedPublicResponseIds.insert(dispute.id)
        }
    }

    func seedDrafts(from disputes: [ReviewDispute]) {
        for dispute in disputes {
            if !locallyEditedPublicResponseIds.contains(dispute.id) {
                publicResponseDrafts[dispute.id] = publicDraftSeed(for: dispute)
            }
            if annotationDrafts[dispute.id] == nil {
                annotationDrafts[dispute.id] = ""
            }
        }
    }

    func setPublicResponseDraft(_ draft: String, for dispute: ReviewDispute) {
        publicResponseDrafts[dispute.id] = draft
        if draft == publicDraftSeed(for: dispute) {
            locallyEditedPublicResponseIds.remove(dispute.id)
        } else {
            locallyEditedPublicResponseIds.insert(dispute.id)
        }
    }

    func setAnnotationDraft(_ draft: String, for dispute: ReviewDispute) {
        annotationDrafts[dispute.id] = draft.prefixByUTF16Length(Self.annotationBodyUTF16Limit)
    }

    func publicDraft(for dispute: ReviewDispute) -> String {
        publicResponseDrafts[dispute.id] ?? publicDraftSeed(for: dispute)
    }

    func annotationDraft(for dispute: ReviewDispute) -> String {
        annotationDrafts[dispute.id] ?? ""
    }

    func annotationBody(for dispute: ReviewDispute) -> String? {
        let body = annotationDraft(for: dispute).trimmingCharacters(in: .whitespacesAndNewlines)
        guard !body.isEmpty, body.utf16.count <= Self.annotationBodyUTF16Limit else { return nil }
        return body
    }

    func message(for error: Error) -> UiVerbatimText {
        if let error = error as? VouchaError {
            return error.errorDescription.map(UiVerbatimText.verbatim)
                ?? .message(.nativeSwiftReviewDisputesGenericError)
        }
        return .verbatim(error.localizedDescription)
    }

    private func publicDraftSeed(for dispute: ReviewDispute) -> String {
        dispute.publicResponse ?? dispute.aiPublicResponse ?? ""
    }
}
