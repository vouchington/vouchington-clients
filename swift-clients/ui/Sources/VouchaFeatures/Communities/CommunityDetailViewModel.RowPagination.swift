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
            return rowPagination.items.map(\.row)
        } catch {
            _ = rowPagination.cancel(request)
            throw error
        }
    }
}
