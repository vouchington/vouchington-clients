import VouchaModels

extension ModerationAppealsViewModel {
    func loadDetail(id: String) async {
        guard canAccess, let client else { return }
        detailRevision += 1
        let revision = detailRevision
        isLoadingDetail = true
        detailError = nil
        errorMessage = nil
        selectedDetailId = id
        detailModerationAppeal = nil
        do {
            let response: ModerationAppealEnvelope = try await client.send(.appeal(id: id))
            try Task.checkCancellation()
            guard revision == detailRevision else { return }
            reconcileAIReruns(from: [response.appeal])
            replace(with: response.appeal, preservingEditedDraft: true)
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
