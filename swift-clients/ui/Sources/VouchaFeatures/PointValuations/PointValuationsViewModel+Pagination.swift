import Foundation
import VouchaCore

extension PointValuationsViewModel {
    var isLoadingMore: Bool {
        pagination.hasLoadedPage && pagination.isLoading && !isLoading
    }

    func load() async {
        guard !isLoading else { return }
        pagination.reset(items: valuations)
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
            let page = try await service.valuations(after: nil)
            guard pagination.isCurrent(request), pagination.complete(
                request,
                items: [],
                endCursor: page.pageInfo.endCursor,
                hasNextPage: page.pageInfo.hasNextPage
            ) else { return }
            valuations = page.results
        } catch {
            guard pagination.fail(request, error: paginationError(error)) else { return }
            errorMessage = .verbatim(error.localizedDescription)
        }
    }

    func loadMore() async {
        guard !isLoading, let request = pagination.beginNextPage() else { return }
        do {
            let page = try await service.valuations(after: request.cursor)
            guard pagination.isCurrent(request), pagination.complete(
                request,
                items: page.results,
                endCursor: page.pageInfo.endCursor,
                hasNextPage: page.pageInfo.hasNextPage
            ) else { return }
            pagination.replaceItems(reconciled(pagination.items))
        } catch {
            pagination.fail(request, error: paginationError(error))
        }
    }

    private func paginationError(_ error: Error) -> VouchaError {
        (error as? VouchaError) ?? .unexpected(error.localizedDescription)
    }
}
