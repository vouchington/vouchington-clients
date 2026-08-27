import VouchaAPI
import VouchaCore
import VouchaModels

extension ListsViewModel {
    func loadItems(preserveItemsOnFailure: Bool = false) async {
        await loadItemsPage(reset: true, preserveItemsOnFailure: preserveItemsOnFailure)
    }

    func loadItemsPage(reset: Bool, preserveItemsOnFailure: Bool = true) async {
        guard let selectedList else {
            itemPagination.reset()
            return
        }
        let requestListId = selectedList.id
        let requestFilter = selectedFilter
        let previousItems = items
        if reset {
            itemPagination.reset()
            itemState = .loading
        }
        guard let request = itemPagination.beginNextPage() else { return }
        do {
            let response: ListItemsResponse = try await client.send(.listItems(
                listId: requestListId,
                mediaType: requestFilter.mediaType,
                after: request.cursor
            ))
            guard isCurrentItemRequest(listId: requestListId, filter: requestFilter),
                  itemPagination.complete(
                      request,
                      items: response.results.compactMap { response.listItems[$0.id] },
                      endCursor: response.pageInfo.endCursor,
                      hasNextPage: response.pageInfo.hasNextPage
                  )
            else { return }
            itemState = .loaded
        } catch let error as VouchaError {
            guard isCurrentItemRequest(listId: requestListId, filter: requestFilter) else { return }
            if reset, preserveItemsOnFailure {
                itemPagination.replaceItems(previousItems)
            }
            _ = itemPagination.fail(request, error: error)
            itemState = .error(error)
        } catch {
            guard isCurrentItemRequest(listId: requestListId, filter: requestFilter) else { return }
            if reset, preserveItemsOnFailure {
                itemPagination.replaceItems(previousItems)
            }
            let error = VouchaError.unexpected(error.localizedDescription)
            _ = itemPagination.fail(request, error: error)
            itemState = .error(error)
        }
    }

    private func isCurrentItemRequest(listId: String, filter: ListsItemFilter) -> Bool {
        selectedList?.id == listId && selectedFilter == filter
    }
}
