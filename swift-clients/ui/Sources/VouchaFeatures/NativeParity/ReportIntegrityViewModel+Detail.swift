import VouchaModels

extension ReportIntegrityViewModel {
    func loadDetail(id: String) async {
        guard canAccess, let service else { return }
        detailRevision += 1
        let revision = detailRevision
        isLoadingDetail = true
        detailFailed = false
        detailFlag = nil
        do {
            let response = try await service.flag(id: id)
            try Task.checkCancellation()
            guard revision == detailRevision else { return }
            detailFlag = response
        } catch is CancellationError {
            guard revision == detailRevision else { return }
        } catch {
            guard revision == detailRevision else { return }
            detailFailed = true
        }
        guard revision == detailRevision else { return }
        isLoadingDetail = false
    }
}
