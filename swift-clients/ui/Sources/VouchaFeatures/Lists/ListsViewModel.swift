import Foundation
import Observation
import VouchaAPI
import VouchaCore
import VouchaModels

@Observable
@MainActor
public final class ListsViewModel {
    var listPagination = CursorPaginationState<UserList>()
    var itemPagination = CursorPaginationState<ListItem>()
    public internal(set) var lists: [UserList] {
        get { listPagination.items }
        set { listPagination.replaceItems(newValue) }
    }

    public internal(set) var items: [ListItem] {
        get { itemPagination.items }
        set { itemPagination.replaceItems(newValue) }
    }

    public internal(set) var selectedList: UserList?
    public internal(set) var state: LoadState = .idle
    public internal(set) var itemState: LoadState = .idle
    public var selectedFilter: ListsItemFilter = .all

    let client: APIClient

    public init(client: APIClient) {
        self.client = client
    }

    public func load() async {
        guard case .idle = state else { return }
        await reload()
    }

    public func reload() async {
        let previousLists = lists
        let previousItems = items
        let previousSelectedList = selectedList
        let previousItemState = itemState
        state = .loading
        listPagination.reset()
        guard let request = listPagination.beginNextPage() else { return }
        do {
            let response: ListsSearchResponse = try await client.send(.lists(after: request.cursor, limit: 25))
            guard listPagination.complete(
                request,
                items: response.results.compactMap { response.lists[$0.id] },
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            ) else { return }
            selectedList = selectedList.flatMap { selected in lists.first { $0.id == selected.id } } ?? lists.first
            state = .loaded
            await loadItems(preserveItemsOnFailure: true)
        } catch let error as VouchaError {
            _ = listPagination.fail(request, error: error)
            restoreReloadSnapshot(
                lists: previousLists,
                items: previousItems,
                selectedList: previousSelectedList,
                itemState: previousItemState
            )
            state = .error(error)
        } catch {
            _ = listPagination.fail(request, error: .unexpected(error.localizedDescription))
            restoreReloadSnapshot(
                lists: previousLists,
                items: previousItems,
                selectedList: previousSelectedList,
                itemState: previousItemState
            )
            state = .error(.api(statusCode: 0, preconditionCode: nil))
        }
    }

    public func selectList(_ list: UserList) async {
        selectedList = list
        await loadItems()
    }

    public func selectFilter(_ filter: ListsItemFilter) async {
        selectedFilter = filter
        await loadItems()
    }

    public func createList(name: String) async {
        let trimmed = name.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return }
        await mutateList {
            let response: ListResponse = try await client.send(.createList(name: trimmed, visibility: "private"))
            lists = [response.list] + lists.filter { $0.id != response.list.id }
            selectedList = response.list
            items = []
            itemState = .loaded
        }
    }

    public func updateSelectedList(name: String, description: String?) async {
        guard let selectedList else { return }
        let requestListId = selectedList.id
        let trimmedName = name.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmedName.isEmpty else { return }
        let trimmedDescription = description?.trimmingCharacters(in: .whitespacesAndNewlines)
        let descriptionPatch: NullableStringPatchField = if let trimmedDescription, !trimmedDescription.isEmpty {
            .value(trimmedDescription)
        } else {
            .null
        }
        await mutateList {
            let response: ListResponse = try await client.send(.updateList(
                listId: selectedList.id,
                name: trimmedName,
                description: descriptionPatch
            ))
            replaceList(response.list, selectingIfCurrent: requestListId)
        }
    }

    public func deleteSelectedList() async {
        guard let selectedList else { return }
        let requestListId = selectedList.id
        await mutateList {
            let _: EmptyResponse = try await client.send(.deleteList(listId: requestListId))
            lists.removeAll { $0.id == requestListId }
            if self.selectedList?.id == requestListId {
                self.selectedList = lists.first
                itemPagination.reset()
                await loadItems()
            }
        }
    }

    public func importCommunity(slug: String) async {
        guard let selectedList else { return }
        let trimmed = slug.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return }
        await mutateList {
            let _: ImportCommunityListResponse = try await client.send(.importCommunityList(
                listId: selectedList.id,
                communitySlug: trimmed
            ))
            await loadItems()
        }
    }

    private func mutateList(_ mutation: () async throws -> Void) async {
        state = .loading
        do {
            try await mutation()
            state = .loaded
        } catch let error as VouchaError {
            state = .error(error)
        } catch {
            state = .error(.api(statusCode: 0, preconditionCode: nil))
        }
    }

    private func replaceList(_ list: UserList, selectingIfCurrent requestListId: String) {
        lists = lists.map { $0.id == list.id ? list : $0 }
        if selectedList?.id == requestListId {
            selectedList = list
        }
    }

}

extension ListsViewModel {
    public var isLoading: Bool {
        if case .loading = state {
            return true
        }
        if case .loading = itemState {
            return true
        }
        return false
    }

    func restoreReloadSnapshot(
        lists: [UserList],
        items: [ListItem],
        selectedList: UserList?,
        itemState: LoadState
    ) {
        guard !lists.isEmpty else {
            self.lists = []
            self.items = []
            self.selectedList = nil
            self.itemState = .idle
            return
        }
        self.lists = lists
        self.items = items
        self.selectedList = selectedList
        self.itemState = itemState
    }
}
