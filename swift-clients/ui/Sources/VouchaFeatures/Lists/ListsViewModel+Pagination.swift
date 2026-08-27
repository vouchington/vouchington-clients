import VouchaAPI
import VouchaCore
import VouchaModels

public extension ListsViewModel {
    func loadMoreLists() async {
        guard let request = listPagination.beginNextPage() else { return }
        do {
            let response: ListsSearchResponse = try await client.send(.lists(after: request.cursor, limit: 25))
            _ = listPagination.complete(
                request,
                items: response.results.compactMap { response.lists[$0.id] },
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            )
        } catch let error as VouchaError {
            _ = listPagination.fail(request, error: error)
        } catch {
            _ = listPagination.fail(request, error: .unexpected(error.localizedDescription))
        }
    }

    func loadMoreItems() async {
        await loadItemsPage(reset: false)
    }
}
