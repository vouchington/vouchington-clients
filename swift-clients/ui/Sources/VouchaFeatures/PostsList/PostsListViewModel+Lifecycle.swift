import VouchaCore

public extension PostsListViewModel {
    /// Reset all pagination state.
    func reset() {
        pagination.reset()
        myVotesByPostId = [:]
        savedPostIds = []
        hiddenPostIds = []
        postEmbedsByPostId = [:]
        hackerNewsReferencesByPostId = [:]
        inFlightVotePostIds = []
        inFlightBookmarkKeys = []
    }

    /// Clear and reload from the first page.
    func reload() async {
        reset()
        await loadNextPage()
    }
}
