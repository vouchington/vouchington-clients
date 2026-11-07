import VouchaModels

extension ReviewDisputesViewModel {
    func loadDetail(id: String) async {
        guard canAccess, let client else { return }
        detailRevision += 1
        let revision = detailRevision
        isLoadingDetail = true
        detailError = nil
        errorMessage = nil
        selectedDetailId = id
        detailReviewDispute = nil
        do {
            let response: ReviewDisputeResponse = try await client.send(.dispute(id: id))
            try Task.checkCancellation()
            guard revision == detailRevision else { return }
            replaceWithTargetedOutcome(response.dispute, preservingUnconfirmedPublicDraft: true)
        } catch is CancellationError {
            guard revision == detailRevision else { return }
        } catch {
            guard revision == detailRevision else { return }
            detailError = message(for: error)
        }
        guard revision == detailRevision else { return }
        isLoadingDetail = false
    }
}
