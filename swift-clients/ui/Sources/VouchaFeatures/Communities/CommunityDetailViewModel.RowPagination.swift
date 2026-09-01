import VouchaAPI
import VouchaCore

extension CommunityDetailViewModel {
    func loadMoreRows() async {
        let revision = communityLoadRevision
        let tab = selectedTab
        guard let client, let request = rowPagination.beginNextPage() else { return }
        do {
            let page = try await loadRows(client: client, after: request.cursor, tab: tab, revision: revision)
            guard isCurrentCommunityLoad(revision, tab: tab) else { return }
            guard rowPagination.complete(
                request,
                items: page.items,
                endCursor: page.endCursor,
                hasNextPage: page.hasMore
            ) else { return }
            mergePostEmbeds(from: page)
            mergeRssFeedItemEmbeds(from: page)
            summary.rows = rowPagination.items.map(\.row)
        } catch let error as VouchaError {
            _ = rowPagination.fail(request, error: error)
        } catch {
            _ = rowPagination.fail(request, error: .unexpected(error.localizedDescription))
        }
    }

    func loadInitialRows(
        client: APIClient,
        tab: CommunitySurfaceTab,
        revision: Int
    ) async throws -> [NativeRouteDestinationRow] {
        guard let request = rowPagination.beginNextPage() else { return [] }
        do {
            let page = try await loadRows(client: client, after: request.cursor, tab: tab, revision: revision)
            guard isCurrentCommunityLoad(revision, tab: tab) else { return [] }
            guard rowPagination.complete(
                request,
                items: page.items,
                endCursor: page.endCursor,
                hasNextPage: page.hasMore
            ) else { return [] }
            replacePostEmbeds(from: page)
            replaceRssFeedItemEmbeds(from: page)
            return rowPagination.items.map(\.row)
        } catch {
            _ = rowPagination.cancel(request)
            throw error
        }
    }

    private func replacePostEmbeds(from page: CommunityForwardPage) {
        guard let postEmbeds = page.postEmbedsByPostId else { return }
        postEmbedsByPostId = postEmbeds
    }

    private func mergePostEmbeds(from page: CommunityForwardPage) {
        guard let postEmbeds = page.postEmbedsByPostId else { return }
        postEmbedsByPostId.merge(postEmbeds) { _, new in new }
    }

    private func replaceRssFeedItemEmbeds(from page: CommunityForwardPage) {
        guard let rssFeedItemEmbeds = page.rssFeedItemEmbedsById else { return }
        rssFeedItemEmbedsById = rssFeedItemEmbeds
    }

    private func mergeRssFeedItemEmbeds(from page: CommunityForwardPage) {
        guard let rssFeedItemEmbeds = page.rssFeedItemEmbedsById else { return }
        rssFeedItemEmbedsById.merge(rssFeedItemEmbeds) { _, new in new }
    }
}
