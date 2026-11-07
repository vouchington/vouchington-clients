import VouchaModels

extension CommunityDetailViewModel {
    func loadApplicationDetail(id: String) async {
        guard canModerateCommunity, let client else { return }
        applicationDetailRevision += 1
        let revision = applicationDetailRevision
        isLoadingApplicationDetail = true
        applicationDetailFailed = false
        applicationDetail = nil
        do {
            let response: CommunityApplicationResponse = try await client.send(
                .communityApplication(idOrSlug: slug, applicationId: id)
            )
            try Task.checkCancellation()
            guard revision == applicationDetailRevision else { return }
            applicationDetail = response.communityApplication
        } catch is CancellationError {
            guard revision == applicationDetailRevision else { return }
        } catch {
            guard revision == applicationDetailRevision else { return }
            applicationDetailFailed = true
        }
        guard revision == applicationDetailRevision else { return }
        isLoadingApplicationDetail = false
    }
}
