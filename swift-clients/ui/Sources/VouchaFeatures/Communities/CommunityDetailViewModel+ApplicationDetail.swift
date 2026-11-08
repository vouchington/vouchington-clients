import VouchaModels

extension CommunityDetailViewModel {
    func discardApplicationReviewDataIfUnauthorized() {
        guard !canModerateCommunity else { return }
        applicationSummaries = [:]
        applicationUsers = [:]
        discardApplicationDetail()
    }

    private func discardApplicationDetail() {
        applicationDetailRevision += 1
        applicationDetail = nil
        applicationDetailFailed = false
        isLoadingApplicationDetail = false
    }

    func loadApplicationDetail(id: String) async {
        guard canModerateCommunity, let client else {
            discardApplicationDetail()
            return
        }
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
            guard canModerateCommunity else {
                isLoadingApplicationDetail = false
                return
            }
            guard response.communityApplication.id == id else {
                throw CommunityApplicationDetailError.mismatchedIdentity
            }
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

private enum CommunityApplicationDetailError: Error {
    case mismatchedIdentity
}
