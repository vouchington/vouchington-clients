import Observation
import VouchaAPI
import VouchaCore

@Observable
@MainActor
final class NativeAiCostsViewModel {
    private(set) var pagination = CursorPaginationState<CommunityAiCostTotal>()
    private(set) var state: LoadState = .idle
    private(set) var isRefreshing = false

    var rows: [CommunityAiCostTotal] {
        pagination.items
    }

    var refreshError: VouchaError? {
        guard !rows.isEmpty, pagination.lastError == nil, case let .error(error) = state else {
            return nil
        }
        return error
    }

    private let client: APIClient
    private var refreshGeneration = 0

    init(client: APIClient) {
        self.client = client
    }

    func loadIfNeeded() async {
        guard !pagination.hasLoadedPage else { return }
        await loadNextPage()
    }

    func refresh() async {
        guard !isRefreshing else { return }
        isRefreshing = true
        defer { isRefreshing = false }
        refreshGeneration += 1
        let generation = refreshGeneration
        pagination.invalidateRequestsPreservingPage()
        state = .loading
        do {
            let response = try await client.adminAiCosts()
            guard generation == refreshGeneration else { return }
            pagination.reset()
            guard let request = pagination.beginNextPage() else { return }
            pagination.complete(
                request,
                items: response.results,
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            )
            state = .loaded
        } catch is CancellationError {
            guard generation == refreshGeneration else { return }
            state = rows.isEmpty ? .idle : .loaded
        } catch let error as VouchaError {
            guard generation == refreshGeneration else { return }
            state = .error(error)
        } catch {
            guard generation == refreshGeneration else { return }
            state = .error(.unexpected(error.localizedDescription))
        }
    }

    func loadNextPage() async {
        guard !isRefreshing else { return }
        guard let request = pagination.beginNextPage() else { return }
        state = .loading
        do {
            let response = try await client.adminAiCosts(after: request.cursor)
            guard pagination.complete(
                request,
                items: response.results,
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            ) else { return }
            state = .loaded
        } catch is CancellationError {
            guard pagination.cancel(request) else { return }
            state = rows.isEmpty ? .idle : .loaded
        } catch let error as VouchaError {
            guard pagination.fail(request, error: error) else { return }
            state = .error(error)
        } catch {
            guard pagination.fail(request, error: .unexpected(error.localizedDescription)) else { return }
            state = .error(.unexpected(error.localizedDescription))
        }
    }
}
