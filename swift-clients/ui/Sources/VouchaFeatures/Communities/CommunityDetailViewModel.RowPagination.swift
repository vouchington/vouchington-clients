import VouchaAPI
import VouchaCore

extension CommunityDetailViewModel {
    func loadMoreRows() async {
        let revision = communityLoadRevision
        let tab = selectedTab
        guard let client, var request = rowPagination.beginNextPage() else { return }
        while true {
            do {
                let page = try await loadRows(client: client, after: request.cursor, tab: tab, revision: revision)
                guard isCurrentCommunityLoad(revision, tab: tab) else { return }
                guard commitRowPage(request, page, replacingEmbeds: false) else { return }
                summary.rows = rowPagination.items.map(\.row)
            } catch let error as VouchaError {
                _ = rowPagination.fail(request, error: error)
                return
            } catch {
                _ = rowPagination.fail(request, error: .unexpected(error.localizedDescription))
                return
            }
            guard let next = rowPagination.continueThroughEmptyPage() else { return }
            request = next
        }
    }

    func loadInitialRows(
        client: APIClient,
        tab: CommunitySurfaceTab,
        revision: Int
    ) async throws -> [NativeRouteDestinationRow] {
        guard var request = rowPagination.beginNextPage() else { return [] }
        var isFirstPage = true
        while true {
            do {
                let page = try await loadRows(client: client, after: request.cursor, tab: tab, revision: revision)
                guard isCurrentCommunityLoad(revision, tab: tab) else { return [] }
                guard commitRowPage(request, page, replacingEmbeds: isFirstPage) else { return [] }
            } catch {
                guard !isFirstPage else {
                    _ = rowPagination.cancel(request)
                    throw error
                }
                // Rows from earlier pages stay; the Load more control retries the failed page.
                _ = rowPagination.fail(request, error: (error as? VouchaError) ?? .unexpected(error.localizedDescription))
                break
            }
            guard let next = rowPagination.continueThroughEmptyPage() else { break }
            request = next
            isFirstPage = false
        }
        return rowPagination.items.map(\.row)
    }

    /// Commits one page and its embeds. Embeds from the first page replace the previous tab's.
    private func commitRowPage(
        _ request: CursorPageRequest,
        _ page: CommunityForwardPage,
        replacingEmbeds: Bool
    ) -> Bool {
        guard rowPagination.complete(
            request,
            items: page.items,
            endCursor: page.endCursor,
            hasNextPage: page.hasMore
        ) else { return false }
        if replacingEmbeds {
            if let postEmbeds = page.postEmbedsByPostId { postEmbedsByPostId = postEmbeds }
            if let itemEmbeds = page.rssFeedItemEmbedsById { rssFeedItemEmbedsById = itemEmbeds }
        } else {
            postEmbedsByPostId.merge(page.postEmbedsByPostId ?? [:]) { _, new in new }
            rssFeedItemEmbedsById.merge(page.rssFeedItemEmbedsById ?? [:]) { _, new in new }
        }
        return true
    }
}
