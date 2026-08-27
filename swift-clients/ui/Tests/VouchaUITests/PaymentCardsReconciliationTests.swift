import Foundation
import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class PaymentCardsReconciliationTests: XCTestCase {
    func testEditingTopicQueryClearsOnlySearchError() {
        let viewModel = PaymentCardsViewModel(
            service: ReconciliationPaymentCardServiceStub(pages: [])
        )
        viewModel.mutationErrorMessage = .verbatim("Create failed")
        viewModel.searchErrorMessage = .verbatim("Search failed")

        viewModel.topicQuery = "edited"

        XCTAssertNotNil(viewModel.mutationErrorMessage)
        XCTAssertNil(viewModel.searchErrorMessage)
    }

    func testEditingSubmittedTopicQueryInvalidatesPendingResults() async throws {
        let service = ReconciliationPaymentCardServiceStub(pages: [])
        let viewModel = PaymentCardsViewModel(service: service)
        let staleResult = try JSONDecoder().decode(
            TopicSearchResult.self,
            from: Data(#"{"__entityType":"topic","id":"topic-1","name":"Stale result"}"#.utf8)
        )

        viewModel.topicQuery = "submitted"
        let staleSearch = Task { await viewModel.searchTopics() }
        await waitUntil { service.searchCalls == 1 }

        viewModel.topicQuery = "changed visible text"
        service.completeSearch(at: 0, with: .success([staleResult]))
        await staleSearch.value

        XCTAssertTrue(viewModel.topicResults.isEmpty)
        XCTAssertFalse(viewModel.isSearching)
    }

    func testBlankSearchInvalidatesPendingSuccessAndFailureWithoutStealingNewSearch() async throws {
        let service = ReconciliationPaymentCardServiceStub(pages: [])
        let viewModel = PaymentCardsViewModel(service: service)
        let staleResult = try JSONDecoder().decode(
            TopicSearchResult.self,
            from: Data(#"{"__entityType":"topic","id":"topic-1","name":"Stale result"}"#.utf8)
        )
        let currentResult = try JSONDecoder().decode(
            TopicSearchResult.self,
            from: Data(#"{"__entityType":"topic","id":"topic-2","name":"Current result"}"#.utf8)
        )
        viewModel.topicResults = [staleResult]
        viewModel.searchErrorMessage = .verbatim("Stale search error")

        viewModel.topicQuery = "stale success"
        let staleSuccess = Task { await viewModel.searchTopics() }
        await waitUntil { service.searchCalls == 1 }
        viewModel.topicQuery = " \n "
        await viewModel.searchTopics()
        XCTAssertTrue(viewModel.topicResults.isEmpty)
        XCTAssertNil(viewModel.searchErrorMessage)
        XCTAssertFalse(viewModel.isSearching)

        viewModel.topicQuery = "replacement"
        let replacementAfterSuccess = Task { await viewModel.searchTopics() }
        await waitUntil { service.searchCalls == 2 }
        service.completeSearch(at: 0, with: .success([staleResult]))
        await staleSuccess.value
        XCTAssertTrue(viewModel.topicResults.isEmpty)
        XCTAssertNil(viewModel.searchErrorMessage)
        XCTAssertTrue(viewModel.isSearching)
        service.completeSearch(at: 1, with: .success([currentResult]))
        await replacementAfterSuccess.value
        XCTAssertEqual(viewModel.topicResults.map(\.id), [currentResult.id])
        XCTAssertFalse(viewModel.isSearching)

        viewModel.topicQuery = "stale failure"
        let staleFailure = Task { await viewModel.searchTopics() }
        await waitUntil { service.searchCalls == 3 }
        viewModel.topicQuery = "\t"
        await viewModel.searchTopics()
        XCTAssertTrue(viewModel.topicResults.isEmpty)
        XCTAssertNil(viewModel.searchErrorMessage)
        XCTAssertFalse(viewModel.isSearching)

        viewModel.topicQuery = "latest"
        let replacementAfterFailure = Task { await viewModel.searchTopics() }
        await waitUntil { service.searchCalls == 4 }
        service.completeSearch(at: 2, with: .failure(ReconciliationTestFailure.unexpected))
        await staleFailure.value
        XCTAssertTrue(viewModel.topicResults.isEmpty)
        XCTAssertNil(viewModel.searchErrorMessage)
        XCTAssertTrue(viewModel.isSearching)
        service.completeSearch(at: 3, with: .success([currentResult]))
        await replacementAfterFailure.value
        XCTAssertEqual(viewModel.topicResults.map(\.id), [currentResult.id])
        XCTAssertNil(viewModel.searchErrorMessage)
        XCTAssertFalse(viewModel.isSearching)
    }

    func testDeletedParentOverlayReconcilesInitialAndContinuationRows() async throws {
        let first = try page("native.cards.page-1")
        let parent = try XCTUnwrap(page("native.cards.page-2").results.first)
        let child = try XCTUnwrap(first.results.first)
        let summaryOnlyChild = replacingParent(
            child, id: "summary-only-child", authorizedUserOfId: nil,
            authorizedUserOfCard: child.authorizedUserOfCard
        )
        let idOnlyChild = replacingParent(
            child, id: "id-only-child", authorizedUserOfId: parent.id,
            authorizedUserOfCard: nil
        )
        let initial = PaymentCardPage(
            results: [parent, summaryOnlyChild],
            pageInfo: .init(hasNextPage: true, endCursor: "next")
        )
        let continuation = PaymentCardPage(results: [idOnlyChild])
        let service = ReconciliationPaymentCardServiceStub(
            pages: [.success(initial), .success(continuation)]
        )
        let viewModel = PaymentCardsViewModel(service: service)
        viewModel.cards = [parent]

        let deleted = await viewModel.delete(parent)
        XCTAssertTrue(deleted)
        await viewModel.load()
        await viewModel.loadMore()

        XCTAssertFalse(viewModel.cards.contains { $0.id == parent.id })
        XCTAssertEqual(Set(viewModel.cards.map(\.id)), Set([summaryOnlyChild.id, idOnlyChild.id]))
        for card in viewModel.cards {
            XCTAssertNil(card.authorizedUserOfId)
            XCTAssertNil(card.authorizedUserOfCard)
        }
    }

    func testLocallySavedCardWinsOverStaleDuplicateFromContinuation() async throws {
        let firstPage = try page("native.cards.page-1")
        let original = try XCTUnwrap(firstPage.results.first)
        let secondPageCard = try XCTUnwrap(page("native.cards.page-2").results.first)
        let continuation = PaymentCardPage(
            results: [original, secondPageCard],
            pageInfo: .init(hasNextPage: false, endCursor: nil)
        )
        let locallySaved = replacingNote(original, note: "Locally saved")
        let service = ReconciliationPaymentCardServiceStub(
            pages: [.success(firstPage), .success(continuation)],
            updatedCard: locallySaved
        )
        let viewModel = PaymentCardsViewModel(service: service)
        await viewModel.load()
        var draft = PaymentCardDraft(card: original)
        draft.note = "Locally saved"

        let saved = await viewModel.save(card: original, draft: draft)
        XCTAssertTrue(saved)
        await viewModel.loadMore()

        XCTAssertEqual(viewModel.cards.first { $0.id == original.id }?.note, "Locally saved")
        XCTAssertEqual(viewModel.cards.count, 3)
    }

    private func replacingParent(
        _ card: PaymentCard,
        id: String,
        authorizedUserOfId: String?,
        authorizedUserOfCard: PaymentCardParentSummary?
    ) -> PaymentCard {
        PaymentCard(
            id: id, cardId: card.cardId, openedOn: card.openedOn, closedOn: card.closedOn,
            receivedSignUpBonusOn: card.receivedSignUpBonusOn, creditLimit: card.creditLimit,
            isAuthorizedUser: card.isAuthorizedUser, authorizedUserOfId: authorizedUserOfId,
            note: card.note, card: card.card, authorizedUserOfCard: authorizedUserOfCard
        )
    }

    private func replacingNote(_ card: PaymentCard, note: String?) -> PaymentCard {
        PaymentCard(
            id: card.id, cardId: card.cardId, openedOn: card.openedOn, closedOn: card.closedOn,
            receivedSignUpBonusOn: card.receivedSignUpBonusOn, creditLimit: card.creditLimit,
            isAuthorizedUser: card.isAuthorizedUser, authorizedUserOfId: card.authorizedUserOfId,
            note: note, card: card.card, authorizedUserOfCard: card.authorizedUserOfCard
        )
    }

    private func page(_ id: String) throws -> PaymentCardPage {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        return try decoder.decode(PaymentCardPage.self, from: ApiFixtureLoader.data(id))
    }

    private func waitUntil(_ condition: @escaping @MainActor () -> Bool) async {
        while !condition() {
            await Task.yield()
        }
    }
}

@MainActor
private final class ReconciliationPaymentCardServiceStub: PaymentCardServicing {
    var pages: [Result<PaymentCardPage, Error>]
    var searchCalls = 0
    private let updatedCard: PaymentCard?
    private var searchCompletions: [(Result<[TopicSearchResult], Error>) -> Void] = []

    init(pages: [Result<PaymentCardPage, Error>], updatedCard: PaymentCard? = nil) {
        self.pages = pages
        self.updatedCard = updatedCard
    }

    func cards(after _: String?) async throws -> PaymentCardPage {
        try pages.removeFirst().get()
    }

    func searchCardTopics(query _: String) async throws -> [TopicSearchResult] {
        searchCalls += 1
        return try await withCheckedThrowingContinuation { continuation in
            searchCompletions.append { continuation.resume(with: $0) }
        }
    }

    func completeSearch(at index: Int, with result: Result<[TopicSearchResult], Error>) {
        searchCompletions[index](result)
    }

    func create(cardId _: String) async throws -> PaymentCard {
        throw ReconciliationTestFailure.unexpected
    }

    func update(id _: String, body _: UpdatePaymentCardBody) async throws -> PaymentCard {
        guard let updatedCard else { throw ReconciliationTestFailure.unexpected }
        return updatedCard
    }

    func delete(id _: String) async throws {}
}

private enum ReconciliationTestFailure: Error {
    case unexpected
}
