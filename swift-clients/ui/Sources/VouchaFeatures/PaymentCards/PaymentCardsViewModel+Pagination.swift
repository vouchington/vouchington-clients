import Foundation
import VouchaCore

extension PaymentCardsViewModel {
    func load() async {
        guard !isLoading else { return }
        let cardsBeforeLoad = Dictionary(uniqueKeysWithValues: cards.map { ($0.id, $0) })
        pagination.reset(items: cards)
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
            let page = try await service.cards(after: nil)
            guard pagination.isCurrent(request) else { return }
            let locallyChangedCards = cards.filter { cardsBeforeLoad[$0.id] != $0 }
            guard pagination.complete(
                request,
                items: [],
                endCursor: page.pageInfo.endCursor,
                hasNextPage: page.pageInfo.hasNextPage
            ) else { return }
            cards = page.results + locallyChangedCards
        } catch {
            guard pagination.fail(request, error: paginationError(error)) else { return }
            errorMessage = .verbatim(error.localizedDescription)
        }
    }

    func loadMore() async {
        guard !isLoading, let request = pagination.beginNextPage() else { return }
        do {
            let page = try await service.cards(after: request.cursor)
            guard pagination.isCurrent(request) else { return }
            let reconciled = reconciledCards(page.results + cards)
            guard pagination.complete(
                request,
                items: page.results,
                endCursor: page.pageInfo.endCursor,
                hasNextPage: page.pageInfo.hasNextPage
            ) else { return }
            pagination.replaceItems(reconciled)
        } catch {
            pagination.fail(request, error: paginationError(error))
        }
    }

    private func paginationError(_ error: Error) -> VouchaError {
        (error as? VouchaError) ?? .unexpected(error.localizedDescription)
    }
}
