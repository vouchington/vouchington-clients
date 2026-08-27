import Foundation
import Observation
import VouchaAPI
import VouchaLocalization
import VouchaModels

@Observable
@MainActor
final class PaymentCardsViewModel {
    var pagination = CursorPaginationState<PaymentCard>()
    var cards: [PaymentCard] {
        get { pagination.items }
        set { pagination.replaceItems(reconciledCards(newValue)) }
    }

    var topicQuery = "" {
        didSet {
            guard topicQuery != oldValue else { return }
            searchGeneration = UUID()
            topicResults = []
            searchErrorMessage = nil
            isSearching = false
        }
    }

    var topicResults: [TopicSearchResult] = []
    var isLoading = false
    var isLoadingMore: Bool {
        pagination.hasLoadedPage && pagination.isLoading && !isLoading
    }

    var isSearching = false
    var isCreating = false
    var mutatingIds: Set<String> = []
    var errorMessage: UiVerbatimText?
    var continuationErrorMessage: UiVerbatimText? {
        guard pagination.hasLoadedPage, pagination.lastError != nil, errorMessage == nil else {
            return nil
        }
        return .message(.nativeSwiftHouseholdsBookmarksPaymentCardsLoadMoreFailed)
    }

    var searchErrorMessage: UiVerbatimText?
    var mutationErrorMessage: UiVerbatimText?

    let service: any PaymentCardServicing
    var endCursor: String? {
        get { pagination.endCursor }
        set { pagination.restoreContinuation(endCursor: newValue, hasMore: pagination.hasMore) }
    }

    var hasNextPage: Bool {
        get { pagination.hasMore }
        set { pagination.restoreContinuation(endCursor: pagination.endCursor, hasMore: newValue) }
    }

    private var deletedCardIds: Set<String> = []
    var searchGeneration = UUID()

    init(service: any PaymentCardServicing) {
        self.service = service
    }

    func create(topicId: String) async {
        guard !isCreating else { return }
        isCreating = true
        mutationErrorMessage = nil
        defer { isCreating = false }
        do {
            let createdCard = try await service.create(cardId: topicId)
            deletedCardIds.remove(createdCard.id)
            cards = reconciledCards(cards + [createdCard])
            mutationErrorMessage = nil
        } catch {
            mutationErrorMessage = .verbatim(error.localizedDescription)
        }
    }

    func save(card: PaymentCard, draft: PaymentCardDraft) async -> Bool {
        guard !mutatingIds.contains(card.id) else { return false }
        mutationErrorMessage = nil
        var reconciledDraft = draft
        reconcileDeletedParentSelection(in: &reconciledDraft)
        let body: VouchaAPI.UpdatePaymentCardBody
        do {
            guard let changes = try reconciledDraft.updateBody(comparedWith: card) else {
                mutationErrorMessage = nil
                return true
            }
            body = changes
        } catch {
            mutationErrorMessage = .message(.nativeSwiftHouseholdsBookmarksPaymentCardsInvalidCreditLimit)
            return false
        }
        mutatingIds.insert(card.id)
        defer { mutatingIds.remove(card.id) }
        do {
            try await replace(service.update(id: card.id, body: body))
            mutationErrorMessage = nil
            return true
        } catch {
            mutationErrorMessage = .verbatim(error.localizedDescription)
            return false
        }
    }

    func delete(_ card: PaymentCard) async -> Bool {
        guard !mutatingIds.contains(card.id) else { return false }
        mutationErrorMessage = nil
        mutatingIds.insert(card.id)
        defer { mutatingIds.remove(card.id) }
        do {
            try await service.delete(id: card.id)
            deletedCardIds.insert(card.id)
            cards = reconciledCards(cards)
            mutationErrorMessage = nil
            return true
        } catch {
            mutationErrorMessage = .verbatim(error.localizedDescription)
            return false
        }
    }

    func reconciledCards(_ values: [PaymentCard]) -> [PaymentCard] {
        Dictionary(values.map { ($0.id, $0) }, uniquingKeysWith: { _, latest in latest })
            .values
            .filter { !deletedCardIds.contains($0.id) }
            .map { $0.clearingDeletedParent(in: deletedCardIds) }
            .sorted { $0.id < $1.id }
    }

    private func replace(_ card: PaymentCard) {
        cards = reconciledCards(cards.filter { $0.id != card.id } + [card])
    }

}

extension PaymentCardsViewModel {
    func reconcileDeletedParentSelection(in draft: inout PaymentCardDraft) {
        guard draft.authorizedUserOfId.map(deletedCardIds.contains) == true else { return }
        draft.authorizedUserOfId = nil
    }
}

private extension PaymentCard {
    func clearingDeletedParent(in deletedIds: Set<String>) -> PaymentCard {
        let referencesDeletedParent = authorizedUserOfId.map(deletedIds.contains) == true ||
            authorizedUserOfCard.map { deletedIds.contains($0.id) } == true
        guard referencesDeletedParent else { return self }
        return PaymentCard(
            id: id, cardId: cardId, openedOn: openedOn, closedOn: closedOn,
            receivedSignUpBonusOn: receivedSignUpBonusOn, creditLimit: creditLimit,
            isAuthorizedUser: isAuthorizedUser, authorizedUserOfId: nil, note: note,
            card: card, authorizedUserOfCard: nil
        )
    }
}
