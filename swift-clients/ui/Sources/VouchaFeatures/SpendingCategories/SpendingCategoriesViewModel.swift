import Foundation
import Observation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

@Observable @MainActor
final class SpendingCategoriesViewModel {
    var pagination = CursorPaginationState<SpendingCategory>()
    var categories: [SpendingCategory] {
        get { pagination.items }
        set { pagination.replaceItems(reconciled(newValue)) }
    }

    var topicQuery = "" {
        didSet { guard topicQuery != oldValue else { return }
            searchGeneration = UUID()
            topicResults = []
            searchErrorMessage = nil
            isSearching = false
        }
    }

    var topicResults: [TopicSearchResult] = []
    var isLoading = false
    var isSearching = false
    var isCreating = false
    var mutatingIds: Set<String> = []
    var errorMessage: UiVerbatimText?
    var searchErrorMessage: UiVerbatimText?
    var mutationErrorMessage: UiVerbatimText?
    var continuationErrorMessage: UiVerbatimText? {
        pagination.hasLoadedPage && pagination
            .lastError != nil && errorMessage == nil ? .message(.nativeSwiftCommonTryAgain) : nil
    }

    let service: any SpendingCategoryServicing
    var localUpserts: [String: SpendingCategory] = [:]
    var deletedIds: Set<String> = []
    private var searchGeneration = UUID()

    init(service: any SpendingCategoryServicing) {
        self.service = service
    }

    func load() async {
        guard !isLoading else { return }
        pagination.reset(items: categories)
        guard let request = pagination.beginNextPage() else { return }
        isLoading = true
        errorMessage = nil
        defer {
            if pagination.isCurrent(request) {
                pagination.cancel(request)
            }
            isLoading = false
        }
        do {
            let page = try await service.categories(after: nil)
            guard pagination.isCurrent(request), pagination.complete(
                request,
                items: page.results,
                endCursor: page.pageInfo.endCursor,
                hasNextPage: page.pageInfo.hasNextPage
            ) else { return }
            categories = page.results
        } catch { guard pagination.fail(request, error: paginationError(error)) else { return }
            errorMessage = .verbatim(error.localizedDescription)
        }
    }

    func loadMore() async {
        guard !isLoading, let request = pagination.beginNextPage() else { return }
        do { let page = try await service.categories(after: request.cursor)
            guard pagination.isCurrent(request), pagination.complete(
                request,
                items: page.results,
                endCursor: page.pageInfo.endCursor,
                hasNextPage: page.pageInfo.hasNextPage
            ) else { return }
            pagination.replaceItems(reconciled(pagination.items))
        } catch { pagination.fail(request, error: paginationError(error)) }
    }

    func searchTopics() async {
        let query = topicQuery.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !query.isEmpty else { searchGeneration = UUID()
            topicResults = []
            searchErrorMessage = nil
            return
        }
        guard !isSearching else { return }
        let generation = UUID()
        searchGeneration = generation
        isSearching = true
        searchErrorMessage = nil
        defer {
            if searchGeneration == generation {
                isSearching = false
            }
        }
        do { let results = try await service.searchTopics(query: query)
            guard searchGeneration == generation,
                  topicQuery.trimmingCharacters(in: .whitespacesAndNewlines) == query else { return }
            var seen = Set<String>()
            topicResults = results.filter { $0.name != nil && seen.insert($0.id).inserted }
        } catch {
            if searchGeneration == generation {
                searchErrorMessage = .verbatim(error.localizedDescription)
            }
        }
    }

    var isLoadingMore: Bool {
        pagination.hasLoadedPage && pagination.isLoading && !isLoading
    }
}
