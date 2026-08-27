import VouchaAPI
import VouchaCore

public extension FriendsListViewModel {
    var shouldRenderList: Bool {
        !items.isEmpty || (pagination(for: tab).hasLoadedPage && hasMore)
    }

    var listError: VouchaError? {
        guard shouldRenderList, !isLoadingMore, case let .error(error) = state else { return nil }
        return error
    }

    var listErrorRetryLoadsInitialPage: Bool {
        !pagination(for: tab).hasLoadedPage
    }

    var loadMoreError: VouchaError? {
        guard pagination(for: tab).hasLoadedPage, !isLoadingMore, case let .error(error) = state else { return nil }
        return error
    }
}
