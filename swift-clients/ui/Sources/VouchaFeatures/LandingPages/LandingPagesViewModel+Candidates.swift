import VouchaAPI

public extension LandingPagesViewModel {
    var hasMoreCandidates: Bool {
        candidatePageInfo?.hasNextPage == true
    }

    var draftItemCount: Int {
        draftItems.reduce(0) { count, item in
            if case let .topicGroup(_, _, entries) = item { return count + 1 + entries.count }
            return count + 1
        }
    }

    var wouldExceedItemLimit: Bool {
        guard let itemLimit else { return false }
        let addedCount = addType == .topicGroup
            ? 1 + selectedGroupReviewIDs.count + selectedGroupReferralLinkIDs.count : 1
        return draftItemCount + addedCount > itemLimit
    }

    func loadMoreCandidates() async {
        guard !isLoading, hasMoreCandidates,
              let cursor = candidatePageInfo?.endCursor, let client = requireClient() else { return }
        isLoadingMoreCandidates = true
        errorMessage = nil
        isItemLimitReached = false
        defer { isLoadingMoreCandidates = false }
        do {
            let response: LandingPageCandidatesResponse = try await client.send(
                .myLandingPageCandidates(after: cursor)
            )
            candidates = LandingPageCandidates(
                canCreateLandingPages: response.candidates.canCreateLandingPages,
                profileLinks: appendUnique(candidates.profileLinks, response.candidates.profileLinks),
                reviews: appendUnique(candidates.reviews, response.candidates.reviews),
                referralLinks: appendUnique(candidates.referralLinks, response.candidates.referralLinks)
            )
            candidatePageInfo = response.pageInfo
            itemLimit = response.itemLimit
            reconcilePickerSelections()
            state = .loaded
        } catch {
            handle(error)
        }
    }
}

private func appendUnique<Item: Identifiable>(_ existing: [Item], _ page: [Item]) -> [Item] {
    var seen = Set(existing.map(\.id))
    return existing + page.filter { seen.insert($0.id).inserted }
}
