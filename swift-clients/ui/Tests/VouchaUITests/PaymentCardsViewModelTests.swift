import Foundation
import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class PaymentCardsViewModelTests: XCTestCase {
    func testLoadsAndAppendsPagesWithoutDuplicates() async throws {
        let service = try PaymentCardServiceStub(pages: [.success(page1()), .success(page2())])
        let viewModel = PaymentCardsViewModel(service: service)

        await viewModel.load()
        await viewModel.loadMore()

        XCTAssertEqual(viewModel.cards.map(\.card.name), ["Freedom Unlimited", "Everyday Cash", "Sapphire Reserve"])
        XCTAssertEqual(Set(viewModel.cards.map(\.id)).count, 3)
        XCTAssertFalse(viewModel.hasNextPage)
        XCTAssertEqual(service.cardCursors.count, 2)
        XCTAssertNil(service.cardCursors[0])
        XCTAssertNotNil(service.cardCursors[1])
    }

    func testLoadMoreFailurePreservesRows() async throws {
        let service = try PaymentCardServiceStub(
            pages: [.success(page1()), .failure(TestFailure.expected), .success(page2())]
        )
        let viewModel = PaymentCardsViewModel(service: service)
        await viewModel.load()
        let originalIds = viewModel.cards.map(\.id)

        await viewModel.loadMore()

        XCTAssertEqual(viewModel.cards.map(\.id), originalIds)
        XCTAssertNil(viewModel.errorMessage)
        XCTAssertEqual(
            uiEnglish(viewModel.continuationErrorMessage),
            "Couldn’t load more cards. Try again."
        )
        XCTAssertTrue(viewModel.hasNextPage)

        await viewModel.loadMore()

        XCTAssertNil(viewModel.continuationErrorMessage)
        XCTAssertEqual(service.cardCursors.count, 3)
    }

    func testRejectsDuplicateMutationsAndStaleLoads() async throws {
        let service = try PaymentCardServiceStub(pages: [.success(page1()), .success(page2())])
        service.delayNanoseconds = 30_000_000
        let viewModel = PaymentCardsViewModel(service: service)

        async let first: Void = viewModel.load()
        async let second: Void = viewModel.load()
        await first
        await second

        XCTAssertEqual(viewModel.cards.map(\.card.name), ["Freedom Unlimited", "Everyday Cash"])
        XCTAssertEqual(service.cardCursors.count, 1)
        service.pages = try [.success(page1())]
        viewModel.hasNextPage = true
        viewModel.endCursor = "cursor"
        async let moreA: Void = viewModel.loadMore()
        async let moreB: Void = viewModel.loadMore()
        await moreA
        await moreB
        XCTAssertEqual(service.cardCursors.count, 2)
    }

    func testRefreshInvalidatesInFlightLoadMoreWithoutLeavingLoadingStateStuck() async throws {
        let firstPage = try page1()
        let secondPage = try page2()
        let service = ControlledPaymentCardServiceStub()
        let viewModel = PaymentCardsViewModel(service: service)
        viewModel.cards = firstPage.results
        viewModel.hasNextPage = true
        viewModel.endCursor = firstPage.pageInfo.endCursor

        let staleLoadMore = Task { await viewModel.loadMore() }
        await waitUntil { service.cardCursors.count == 1 }
        let refresh = Task { await viewModel.load() }
        await waitUntil { service.cardCursors.count == 2 }
        XCTAssertFalse(viewModel.isLoadingMore)
        await viewModel.loadMore()
        XCTAssertEqual(service.cardCursors.count, 2)

        service.completeRequest(at: 1, with: firstPage)
        await refresh.value
        let currentLoadMore = Task { await viewModel.loadMore() }
        await waitUntil { service.cardCursors.count == 3 }
        XCTAssertTrue(viewModel.isLoadingMore)

        service.completeRequest(at: 0, with: secondPage)
        await staleLoadMore.value
        XCTAssertTrue(viewModel.isLoadingMore)
        XCTAssertEqual(viewModel.cards.map(\.id), firstPage.results.map(\.id))

        service.completeRequest(at: 2, with: secondPage)
        await currentLoadMore.value

        XCTAssertFalse(viewModel.isLoadingMore)
        XCTAssertEqual(viewModel.cards.count, 3)
    }

    func testCreateDuringInitialLoadIsPreservedWhenPageArrives() async throws {
        let service = try PaymentCardServiceStub(pages: [.success(page1())])
        service.delayNanoseconds = 30_000_000
        let viewModel = PaymentCardsViewModel(service: service)

        async let load: Void = viewModel.load()
        await waitUntil { viewModel.isLoading }
        await viewModel.create(topicId: "new-card-topic")
        await load

        XCTAssertEqual(
            viewModel.cards.map(\.card.name),
            ["Freedom Unlimited", "Everyday Cash", "Sapphire Reserve"]
        )
    }

    func testDeleteDuringRefreshIsNotResurrectedByStalePage() async throws {
        let stalePage = try page1()
        let deletedCard = stalePage.results[0]
        let service = PaymentCardServiceStub(pages: [.success(stalePage)])
        service.delayNanoseconds = 30_000_000
        let viewModel = PaymentCardsViewModel(service: service)
        viewModel.cards = stalePage.results

        async let load: Void = viewModel.load()
        await waitUntil { viewModel.isLoading }
        let deleted = await viewModel.delete(deletedCard)
        await load

        XCTAssertTrue(deleted)
        XCTAssertFalse(viewModel.cards.contains { $0.id == deletedCard.id })
        XCTAssertEqual(viewModel.cards.map(\.card.name), ["Everyday Cash"])
        XCTAssertEqual(service.deletedIds, [deletedCard.id])
    }

    func testNoOpSaveSkipsPatchAndFailedSavePreservesDraft() async throws {
        let service = PaymentCardServiceStub(pages: [])
        let card = try page1().results[0]
        let viewModel = PaymentCardsViewModel(service: service)
        var draft = PaymentCardDraft(card: card)

        let noOpSucceeded = await viewModel.save(card: card, draft: draft)
        XCTAssertTrue(noOpSucceeded)
        XCTAssertEqual(service.updateCalls, 0)

        draft.note = "Keep this draft"
        service.updateError = TestFailure.expected
        let failedSave = await viewModel.save(card: card, draft: draft)
        XCTAssertFalse(failedSave)
        XCTAssertEqual(draft.note, "Keep this draft")
        XCTAssertEqual(service.updateCalls, 1)
        XCTAssertNotNil(viewModel.mutationErrorMessage)
    }

    func testLocaleAwareDraftIsNoOpAndUnrelatedEditsDoNotPatchCreditLimit() async throws {
        let original = try page1().results[0]
        let card = try replacing(
            original,
            creditLimit: Money(amount: 123_450, currency: "usd")
        )
        let service = PaymentCardServiceStub(pages: [])
        let viewModel = PaymentCardsViewModel(service: service)

        for identifier in ["fr_FR", "es_ES", "pt_PT"] {
            let locale = Locale(identifier: identifier)
            let draft = PaymentCardDraft(card: card, locale: locale)
            let saved = await viewModel.save(card: card, draft: draft)
            XCTAssertTrue(saved)
        }
        XCTAssertEqual(service.updateCalls, 0)

        let locale = Locale(identifier: "fr_FR")
        var draft = PaymentCardDraft(card: card, locale: locale)
        draft.openedOn = LocalDate(year: 2_024, month: 2, day: 1)
        let saved = await viewModel.save(card: card, draft: draft)
        XCTAssertTrue(saved)
        let body = try XCTUnwrap(service.updatedBodies.last)
        XCTAssertNil(body.creditLimit)
    }

    func testCreditLimitRejectsPartiallyParsedNumbers() throws {
        let card = try replacing(page1().results[0], creditLimit: nil)

        for text in ["12abc", "1,234abc", "1.2.3"] {
            var draft = PaymentCardDraft(card: card, locale: Locale(identifier: "en_US"))
            draft.creditLimitText = text

            XCTAssertThrowsError(
                try draft.updateBody(comparedWith: card),
                "Expected \(text) to be rejected"
            )
        }
    }

    func testCreditLimitAcceptsCompleteLocalizedNumbers() throws {
        let card = try replacing(page1().results[0], creditLimit: nil)
        let cases = [
            ("en_US", "1,234.5", 123_450),
            ("fr_FR", "1 234,5", 123_450),
            ("en_US", "0", 0)
        ]

        for (localeIdentifier, text, expectedAmount) in cases {
            let locale = Locale(identifier: localeIdentifier)
            var draft = PaymentCardDraft(card: card, locale: locale)
            draft.creditLimitText = text

            let body = try XCTUnwrap(draft.updateBody(comparedWith: card))
            let object = try XCTUnwrap(
                JSONSerialization.jsonObject(with: JSONEncoder().encode(body)) as? [String: Any]
            )
            let creditLimit = try XCTUnwrap(object["creditLimit"] as? [String: Any])
            XCTAssertEqual((creditLimit["amount"] as? NSNumber)?.int64Value, Int64(expectedAmount))
            XCTAssertEqual(creditLimit["currency"] as? String, "usd")
        }
    }

    func testNonemptyNoteIsPreservedVerbatimIncludingWhitespace() throws {
        let card = try page1().results[1]
        var draft = PaymentCardDraft(card: card)
        draft.note = "  keep surrounding whitespace  \n"

        let body = try XCTUnwrap(draft.updateBody(comparedWith: card))
        let object = try XCTUnwrap(
            JSONSerialization.jsonObject(with: JSONEncoder().encode(body)) as? [String: Any]
        )
        XCTAssertEqual(object["note"] as? String, "  keep surrounding whitespace  \n")
    }

    func testPreventsDuplicateCreatesSearchesAndUpdates() async throws {
        let card = try page1().results[0]
        let service = PaymentCardServiceStub(pages: [])
        service.mutationDelayNanoseconds = 30_000_000
        let viewModel = PaymentCardsViewModel(service: service)

        viewModel.topicQuery = "Freedom"
        async let searchA: Void = viewModel.searchTopics()
        async let searchB: Void = viewModel.searchTopics()
        await searchA
        await searchB
        XCTAssertEqual(service.searchCalls, 1)

        async let createA: Void = viewModel.create(topicId: card.cardId)
        async let createB: Void = viewModel.create(topicId: card.cardId)
        await createA
        await createB
        XCTAssertEqual(service.createCalls, 1)

        var draft = PaymentCardDraft(card: card)
        draft.note = "Updated"
        let updatedDraft = draft
        async let updateA = viewModel.save(card: card, draft: updatedDraft)
        async let updateB = viewModel.save(card: card, draft: updatedDraft)
        let updateResults = await [updateA, updateB]
        XCTAssertEqual(service.updateCalls, 1)
        XCTAssertEqual(updateResults.filter { $0 }.count, 1)
    }

    func testAuthorizedUserFilteringAndClearing() throws {
        let first = try page1()
        let parent = try page2().results[0]
        let child = first.results[0]
        let service = PaymentCardServiceStub(pages: [])
        let viewModel = PaymentCardsViewModel(service: service)
        viewModel.cards = first.results + [parent]

        XCTAssertEqual(
            viewModel.parentChoices(for: child, selectedParentId: child.authorizedUserOfId).map(\.card.name),
            ["Sapphire Reserve"]
        )
        viewModel.cards = [child]
        XCTAssertEqual(
            viewModel.parentChoices(for: child, selectedParentId: child.authorizedUserOfId).map(\.card.name),
            ["Sapphire Reserve"]
        )
        XCTAssertTrue(viewModel.parentChoices(for: child, selectedParentId: nil).isEmpty)

        var draft = PaymentCardDraft(card: child)
        draft.isAuthorizedUser = false
        let body = try XCTUnwrap(draft.updateBody(comparedWith: child))
        XCTAssertEqual(body.isAuthorizedUser, false)
        XCTAssertNotNil(body.authorizedUserOfId)
    }

    func testDeletingParentClearsChildRelationship() async throws {
        let first = try page1()
        let parent = try page2().results[0]
        let service = PaymentCardServiceStub(pages: [])
        let viewModel = PaymentCardsViewModel(service: service)
        viewModel.cards = first.results + [parent]

        let deleted = await viewModel.delete(parent)
        XCTAssertTrue(deleted)

        XCTAssertFalse(viewModel.cards.contains { $0.id == parent.id })
        XCTAssertNil(viewModel.cards.first { $0.id == first.results[0].id }?.authorizedUserOfId)
        XCTAssertEqual(service.deletedIds, [parent.id])
    }

    func testMutationErrorsClearOnRetrySuccessAndNoOp() async throws {
        let card = try page1().results[0]
        let service = PaymentCardServiceStub(pages: [])
        let viewModel = PaymentCardsViewModel(service: service)

        service.searchError = TestFailure.expected
        viewModel.topicQuery = "Freedom"
        await viewModel.searchTopics()
        XCTAssertNotNil(viewModel.searchErrorMessage)
        service.searchError = nil
        await viewModel.searchTopics()
        XCTAssertNil(viewModel.searchErrorMessage)

        service.createError = TestFailure.expected
        await viewModel.create(topicId: card.cardId)
        XCTAssertNotNil(viewModel.mutationErrorMessage)
        service.createError = nil
        await viewModel.create(topicId: card.cardId)
        XCTAssertNil(viewModel.mutationErrorMessage)

        var draft = PaymentCardDraft(card: card)
        draft.note = "Updated"
        service.updateError = TestFailure.expected
        let failedSave = await viewModel.save(card: card, draft: draft)
        XCTAssertFalse(failedSave)
        service.updateError = nil
        let successfulSave = await viewModel.save(card: card, draft: draft)
        XCTAssertTrue(successfulSave)
        XCTAssertNil(viewModel.mutationErrorMessage)

        viewModel.mutationErrorMessage = .verbatim("old")
        let noOpSave = await viewModel.save(card: card, draft: PaymentCardDraft(card: card))
        XCTAssertTrue(noOpSave)
        XCTAssertNil(viewModel.mutationErrorMessage)

        service.deleteError = TestFailure.expected
        let failedDelete = await viewModel.delete(card)
        XCTAssertFalse(failedDelete)
        service.deleteError = nil
        let successfulDelete = await viewModel.delete(card)
        XCTAssertTrue(successfulDelete)
        XCTAssertNil(viewModel.mutationErrorMessage)
    }

    private func replacing(_ card: PaymentCard, creditLimit: Money?) -> PaymentCard {
        PaymentCard(
            id: card.id, cardId: card.cardId, openedOn: card.openedOn, closedOn: card.closedOn,
            receivedSignUpBonusOn: card.receivedSignUpBonusOn, creditLimit: creditLimit,
            isAuthorizedUser: card.isAuthorizedUser, authorizedUserOfId: card.authorizedUserOfId,
            note: card.note, card: card.card, authorizedUserOfCard: card.authorizedUserOfCard
        )
    }

    private func page1() throws -> PaymentCardPage {
        try decoder.decode(PaymentCardPage.self, from: ApiFixtureLoader.data("native.cards.page-1"))
    }

    private func page2() throws -> PaymentCardPage {
        try decoder.decode(PaymentCardPage.self, from: ApiFixtureLoader.data("native.cards.page-2"))
    }

    private var decoder: JSONDecoder {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        return decoder
    }

    private func waitUntil(_ condition: @escaping @MainActor () -> Bool) async {
        while !condition() {
            await Task.yield()
        }
    }
}

@MainActor
private final class ControlledPaymentCardServiceStub: PaymentCardServicing {
    var cardCursors: [String?] = []
    private var pageCompletions: [(PaymentCardPage) -> Void] = []

    func cards(after cursor: String?) async throws -> PaymentCardPage {
        await withCheckedContinuation { continuation in
            cardCursors.append(cursor)
            pageCompletions.append { continuation.resume(returning: $0) }
        }
    }

    func completeRequest(at index: Int, with page: PaymentCardPage) {
        pageCompletions[index](page)
    }

    func searchCardTopics(query _: String) async throws -> [TopicSearchResult] {
        []
    }

    func create(cardId _: String) async throws -> PaymentCard {
        throw TestFailure.expected
    }

    func update(id _: String, body _: UpdatePaymentCardBody) async throws -> PaymentCard {
        throw TestFailure.expected
    }

    func delete(id _: String) async throws {
        throw TestFailure.expected
    }
}

@MainActor
private final class PaymentCardServiceStub: PaymentCardServicing {
    var pages: [Result<PaymentCardPage, Error>]
    var delayNanoseconds: UInt64 = 0
    var mutationDelayNanoseconds: UInt64 = 0
    var updateError: Error?
    var searchError: Error?
    var createError: Error?
    var deleteError: Error?
    var cardCursors: [String?] = []
    var updateCalls = 0
    var searchCalls = 0
    var createCalls = 0
    var deletedIds: [String] = []
    var updatedBodies: [UpdatePaymentCardBody] = []

    init(pages: [Result<PaymentCardPage, Error>]) {
        self.pages = pages
    }

    func cards(after: String?) async throws -> PaymentCardPage {
        cardCursors.append(after)
        let result = pages.removeFirst()
        if delayNanoseconds > 0 {
            try await Task.sleep(nanoseconds: delayNanoseconds)
        }
        return try result.get()
    }

    func searchCardTopics(query _: String) async throws -> [TopicSearchResult] {
        searchCalls += 1
        if mutationDelayNanoseconds > 0 {
            try await Task.sleep(nanoseconds: mutationDelayNanoseconds)
        }
        if let searchError {
            throw searchError
        }
        return []
    }

    func create(cardId _: String) async throws -> PaymentCard {
        createCalls += 1
        if mutationDelayNanoseconds > 0 {
            try await Task.sleep(nanoseconds: mutationDelayNanoseconds)
        }
        if let createError {
            throw createError
        }
        return try XCTUnwrap(try makeCardPage("native.cards.page-2").results.first)
    }

    func update(id _: String, body: UpdatePaymentCardBody) async throws -> PaymentCard {
        updateCalls += 1
        updatedBodies.append(body)
        if mutationDelayNanoseconds > 0 {
            try await Task.sleep(nanoseconds: mutationDelayNanoseconds)
        }
        if let updateError {
            throw updateError
        }
        return try XCTUnwrap(try makeCardPage("native.cards.page-2").results.first)
    }

    func delete(id: String) async throws {
        deletedIds.append(id)
        if let deleteError {
            throw deleteError
        }
    }

    private func makeCardPage(_ id: String) throws -> PaymentCardPage {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        return try decoder.decode(PaymentCardPage.self, from: ApiFixtureLoader.data(id))
    }
}

private enum TestFailure: Error {
    case expected
}
