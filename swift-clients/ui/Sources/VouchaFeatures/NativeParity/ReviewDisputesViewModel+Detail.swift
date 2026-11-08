import VouchaModels

extension ReviewDisputesViewModel {
    func loadDetail(id: String) async {
        guard canAccess, let client else { return }
        detailRevision += 1
        let revision = detailRevision
        let expectedOutcomeRevision = targetedOutcomeRevision
        isLoadingDetail = true
        defer {
            if revision == detailRevision { isLoadingDetail = false }
        }
        detailError = nil
        errorMessage = nil
        selectedDetailId = id
        detailReviewDispute = nil
        do {
            let response: ReviewDisputeResponse = try await client.send(.dispute(id: id))
            try Task.checkCancellation()
            guard revision == detailRevision,
                  expectedOutcomeRevision == targetedOutcomeRevision || detailReviewDispute?.id != id
            else { return }
            replaceWithTargetedOutcome(response.dispute, preservingUnconfirmedPublicDraft: true)
        } catch is CancellationError {
            guard revision == detailRevision else { return }
        } catch {
            guard revision == detailRevision,
                  expectedOutcomeRevision == targetedOutcomeRevision || detailReviewDispute?.id != id
            else { return }
            detailError = message(for: error)
        }
    }
}
