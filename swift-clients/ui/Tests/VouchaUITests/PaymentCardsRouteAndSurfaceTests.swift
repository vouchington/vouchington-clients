import Foundation
import SwiftUI
import ViewInspector
import VouchaAPI
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class PaymentCardsRouteAndSurfaceTests: XCTestCase {
    func testMyCardsHasDedicatedAuthenticatedDestination() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/cards"))
        XCTAssertEqual(route.entry.destinationIdentifier, .paymentCards)
        XCTAssertEqual(route.match.template, "/my/cards")
        XCTAssertFalse(NativeRouteDestinationIdentifier.paymentCards.supportsRemoteNativeSurface)

        let profile = try XCTUnwrap(
            NativeRouteCatalog.includedEntries.first { $0.destinationIdentifier == .profileSettings }
        )
        XCTAssertFalse(profile.patterns.contains { $0.template == "/my/cards" })
        let signedOut = NativeRouteDestinationView(
            entry: route.entry,
            routeMatch: route.match,
            isSignedIn: false
        )
        XCTAssertNoThrow(try signedOut.inspect().find(text: "Sign in required"))
        XCTAssertThrowsError(try signedOut.inspect().find(text: "Add a card"))
    }

    func testSurfaceRendersHumanNamesAndNativeControlsWithoutUUIDs() throws {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        let page = try decoder.decode(
            PaymentCardPage.self,
            from: ApiFixtureLoader.data("native.cards.page-1")
        )
        let viewModel = PaymentCardsViewModel(service: RoutePaymentCardServiceStub())
        viewModel.cards = page.results
        let bonusCard = PaymentCard(
            id: "bonus-card", cardId: page.results[1].cardId,
            openedOn: nil, closedOn: nil,
            receivedSignUpBonusOn: LocalDate(year: 2_024, month: 6, day: 1),
            creditLimit: nil, isAuthorizedUser: true, authorizedUserOfId: nil,
            note: nil, card: page.results[1].card, authorizedUserOfCard: nil
        )
        viewModel.cards.append(bonusCard)
        let sut = PaymentCardsSurface(viewModel: viewModel)
            .environment(\.locale, Locale(identifier: "en_US"))
        let inspected = try sut.inspect()
        let renderedTexts = try inspected.findAll(ViewType.Text.self).map { try $0.string() }

        XCTAssertNoThrow(try inspected.find(text: "Freedom Unlimited"))
        XCTAssertNoThrow(try inspected.find(text: "Sapphire Reserve"))
        XCTAssertNoThrow(try inspected.find(text: "Opened on:"))
        XCTAssertTrue(renderedTexts.contains("Jan 20, 2024"), "Rendered texts: \(renderedTexts)")
        XCTAssertNoThrow(try inspected.find(text: "Closed on:"))
        XCTAssertTrue(renderedTexts.contains("Dec 31, 2025"), "Rendered texts: \(renderedTexts)")
        XCTAssertNoThrow(try inspected.find(text: "Sign-up bonus received on:"))
        XCTAssertTrue(renderedTexts.contains("Jun 1, 2024"), "Rendered texts: \(renderedTexts)")
        XCTAssertNoThrow(try inspected.find(text: "Credit limit:"))
        XCTAssertNoThrow(try inspected.find(text: "$0.00"))
        XCTAssertNoThrow(try inspected.find(text: "Authorized user of:"))
        XCTAssertNoThrow(try inspected.find(text: "Authorized user"))
        XCTAssertNoThrow(try inspected.find(text: "Note:"))
        XCTAssertNoThrow(try inspected.find(text: "Authorized-user account"))
        XCTAssertThrowsError(try inspected.find(text: page.results[0].id))
        XCTAssertNoThrow(try inspected.find(button: "Edit"))

        var draft = PaymentCardDraft(card: page.results[0])
        let editor = PaymentCardEditor(
            draft: Binding(get: { draft }, set: { draft = $0 }),
            parentChoices: viewModel.parentChoices(
                for: page.results[0],
                selectedParentId: draft.authorizedUserOfId
            ),
            canLoadMoreParents: true,
            isSaving: false,
            loadMoreParents: {}, save: {}, cancel: {}
        )
        let editorInspection = try editor.inspect()
        XCTAssertNoThrow(try editorInspection.find(text: "Opened on"))
        XCTAssertNoThrow(try editorInspection.find(text: "Closed on"))
        XCTAssertNoThrow(try editorInspection.find(text: "Sign-up bonus received on"))
        XCTAssertNoThrow(try editorInspection.find(ViewType.DatePicker.self))
        XCTAssertNoThrow(try editorInspection.find(ViewType.TextField.self))
        XCTAssertNoThrow(try editorInspection.find(text: "Authorized user"))
    }

    func testCreditLimitRowsFormatUsdAndJpyAsCurrency() throws {
        let original = try paymentCardPage("native.cards.page-1").results[0]
        let viewModel = PaymentCardsViewModel(service: RoutePaymentCardServiceStub())
        let cases = try [
            (Money(amount: 123_450, currency: "usd"), "$1,234.50"),
            (Money(amount: 1_234, currency: "jpy"), "¥1,234")
        ]

        for (creditLimit, expected) in cases {
            let card = replacing(original, creditLimit: creditLimit)
            let sut = PaymentCardRow(card: card, viewModel: viewModel)
                .environment(\.locale, Locale(identifier: "en_US"))

            XCTAssertNoThrow(try sut.inspect().find(text: expected))
        }
    }

    func testCreditLimitEditorKeepsPlainMajorUnitValuesForUsdAndJpy() throws {
        let original = try paymentCardPage("native.cards.page-1").results[0]
        let cases = try [
            (Money(amount: 123_450, currency: "usd"), "1,234.5"),
            (Money(amount: 1_234, currency: "jpy"), "1,234")
        ]

        for (creditLimit, expected) in cases {
            var draft = PaymentCardDraft(
                card: replacing(original, creditLimit: creditLimit),
                locale: Locale(identifier: "en_US")
            )
            let editor = PaymentCardEditor(
                draft: Binding(get: { draft }, set: { draft = $0 }),
                parentChoices: [],
                canLoadMoreParents: false,
                isSaving: false,
                loadMoreParents: {},
                save: {},
                cancel: {}
            )

            XCTAssertNoThrow(
                try editor.inspect().find(
                    ViewType.TextField.self,
                    where: { try $0.input() == expected }
                )
            )
            XCTAssertFalse(draft.creditLimitText.contains("$"))
            XCTAssertFalse(draft.creditLimitText.contains("¥"))
        }
    }

    func testDeletionRequiresConfirmation() throws {
        let source = try repoSource(
            "swift-clients/ui/Sources/VouchaFeatures/PaymentCards/PaymentCardRow.swift"
        )
        XCTAssertTrue(source.contains(".confirmationDialog("))
        XCTAssertTrue(source.contains("role: .destructive"))
        XCTAssertTrue(source.contains("role: .cancel"))
        XCTAssertTrue(source.contains("PaymentCardsConfirm"))
        XCTAssertTrue(source.contains("draft.synchronizeAuthorizedUserParent("))
    }

    func testOpenDraftReconcilesDeletedParentWithoutDiscardingUserEdits() throws {
        let firstPage = try paymentCardPage("native.cards.page-1")
        let child = firstPage.results[0]
        let parent = try paymentCardPage("native.cards.page-2").results[0]
        var draft = PaymentCardDraft(card: child)
        draft.note = "Editing in progress"
        XCTAssertEqual(draft.authorizedUserOfId, parent.id)

        draft.synchronizeAuthorizedUserParent(from: parent.id, to: nil)

        XCTAssertNil(draft.authorizedUserOfId)
        XCTAssertEqual(draft.note, "Editing in progress")

        var userChangedDraft = PaymentCardDraft(card: child)
        userChangedDraft.authorizedUserOfId = "replacement-parent"
        userChangedDraft.synchronizeAuthorizedUserParent(from: parent.id, to: nil)

        XCTAssertEqual(userChangedDraft.authorizedUserOfId, "replacement-parent")
    }

    func testMountedEditorClearsUnsavedDeletedParentBeforeSave() async throws {
        let page = try paymentCardPage("native.cards.page-1")
        let child = page.results[0]
        let unsavedParent = page.results[1]
        let service = RoutePaymentCardServiceStub(updatedCard: child)
        let viewModel = PaymentCardsViewModel(service: service)
        viewModel.cards = [child, unsavedParent]
        let editorState = PaymentCardRowEditorState(card: child)
        let sut = PaymentCardRow(
            card: child,
            viewModel: viewModel,
            editorState: editorState
        )

        try await ViewHosting.host(sut) {
            let authorizedUserPicker = {
                try sut.inspect().find(ViewType.Picker.self, where: {
                    try $0.labelView().text().string() == "Authorized user of"
                })
            }
            try sut.inspect().find(button: "Edit").tap()
            await Task.yield()
            XCTAssertTrue(editorState.isEditing)
            let picker = try authorizedUserPicker()
            XCTAssertEqual(try picker.labelView().text().string(), "Authorized user of")
            try picker.select(value: Optional(unsavedParent.id))
            XCTAssertEqual(try picker.selectedValue(String?.self), unsavedParent.id)
            XCTAssertEqual(editorState.draft.authorizedUserOfId, unsavedParent.id)

            let deleted = await viewModel.delete(unsavedParent)
            await Task.yield()
            XCTAssertTrue(deleted)
            XCTAssertNil(editorState.draft.authorizedUserOfId)
            XCTAssertNil(
                try authorizedUserPicker().selectedValue(String?.self)
            )

            try sut.inspect().find(button: "Save").tap()
            await waitUntil { !service.updatedBodies.isEmpty }
            let body = try XCTUnwrap(service.updatedBodies.last)
            XCTAssertFalse(
                try String(decoding: JSONEncoder().encode(body), as: UTF8.self)
                    .contains(unsavedParent.id)
            )
        }
    }

    func testEditorDateBindingUsesLocalGregorianDayInNonGregorianEnvironment() throws {
        let page = try paymentCardPage("native.cards.page-1")
        var draft = PaymentCardDraft(card: page.results[0])
        var islamicCalendar = Calendar(identifier: .islamicUmmAlQura)
        islamicCalendar.timeZone = try XCTUnwrap(TimeZone(secondsFromGMT: 14 * 60 * 60))
        var localGregorianCalendar = Calendar(identifier: .gregorian)
        localGregorianCalendar.timeZone = islamicCalendar.timeZone
        let selectedDate = try XCTUnwrap(
            localGregorianCalendar.date(from: DateComponents(year: 2_024, month: 2, day: 29))
        )
        let localDateBinding = Binding<LocalDate?>(
            get: { draft.openedOn },
            set: { draft.openedOn = $0 }
        )
        let dateBinding = paymentCardDatePickerBinding(
            localDateBinding, timeZone: islamicCalendar.timeZone
        )
        let picker = DatePicker("Opened on", selection: dateBinding, displayedComponents: .date)
            .environment(\.calendar, islamicCalendar)
            .environment(\.locale, Locale(identifier: "ar_SA"))
            .environment(\.timeZone, islamicCalendar.timeZone)

        try picker.inspect().find(ViewType.DatePicker.self).select(date: selectedDate)

        XCTAssertEqual(draft.openedOn, LocalDate(year: 2_024, month: 2, day: 29))
        XCTAssertEqual(dateBinding.wrappedValue, selectedDate)
    }

    func testContinuationFailureRendersRetryThatLoadsNextPage() async throws {
        let page = try paymentCardPage("native.cards.page-1")
        let service = try RoutePaymentCardServiceStub(pages: [
            .failure(RouteTestFailure.unexpected),
            .success(paymentCardPage("native.cards.page-2"))
        ])
        let viewModel = PaymentCardsViewModel(service: service)
        viewModel.cards = page.results
        viewModel.hasNextPage = true
        viewModel.endCursor = page.pageInfo.endCursor
        await viewModel.loadMore()
        let sut = PaymentCardsSurface(viewModel: viewModel)
        let inspected = try sut.inspect()

        XCTAssertNoThrow(try inspected.find(text: "Freedom Unlimited"))
        XCTAssertNoThrow(try inspected.find(text: "Couldn’t load more cards. Try again."))
        XCTAssertThrowsError(try inspected.find(button: "Load more"))
        XCTAssertNoThrow(
            try inspected.find(viewWithAccessibilityIdentifier: "payment-cards-pagination")
        )
        try inspected.find(button: "Try Again").tap()
        await waitUntil { service.cardCursors.count == 2 }

        XCTAssertEqual(service.cardCursors, [page.pageInfo.endCursor, page.pageInfo.endCursor])
        XCTAssertNil(viewModel.continuationErrorMessage)
    }

    func testInitialLoadFailureRendersRetryAlongsideLocallyCreatedCard() throws {
        let page = try paymentCardPage("native.cards.page-1")
        let viewModel = PaymentCardsViewModel(service: RoutePaymentCardServiceStub())
        viewModel.cards = [page.results[0]]
        viewModel.errorMessage = .verbatim("Initial load failed")

        let inspected = try PaymentCardsSurface(viewModel: viewModel).inspect()

        XCTAssertNoThrow(try inspected.find(text: "Freedom Unlimited"))
        XCTAssertNoThrow(try inspected.find(text: "Initial load failed"))
        XCTAssertNoThrow(try inspected.find(button: "Try Again"))
    }

    func testPaymentCardsRouteIdentityRestoresAndRecreatesWithoutChangingRoute() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/cards"))
        let signedOut = NativeRouteDestinationView(
            entry: route.entry, routeMatch: route.match, isSignedIn: false
        )
        let restored = NativeRouteDestinationView(
            entry: route.entry, routeMatch: route.match, isSignedIn: true
        )
        let recreatedAfterBack = NativeRouteDestinationView(
            entry: route.entry, routeMatch: route.match, isSignedIn: true
        )

        XCTAssertNotEqual(signedOut.routeIdentity, restored.routeIdentity)
        XCTAssertEqual(restored.routeIdentity, recreatedAfterBack.routeIdentity)
        XCTAssertTrue(restored.routeIdentity.contains("payment-cards"))
    }

    private func paymentCardPage(_ id: String) throws -> PaymentCardPage {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        return try decoder.decode(PaymentCardPage.self, from: ApiFixtureLoader.data(id))
    }

    private func replacing(_ card: PaymentCard, creditLimit: Money?) -> PaymentCard {
        PaymentCard(
            id: card.id,
            cardId: card.cardId,
            openedOn: card.openedOn,
            closedOn: card.closedOn,
            receivedSignUpBonusOn: card.receivedSignUpBonusOn,
            creditLimit: creditLimit,
            isAuthorizedUser: card.isAuthorizedUser,
            authorizedUserOfId: card.authorizedUserOfId,
            note: card.note,
            card: card.card,
            authorizedUserOfCard: card.authorizedUserOfCard
        )
    }

    private func waitUntil(_ condition: @escaping @MainActor () -> Bool) async {
        for _ in 0 ..< 100 where !condition() {
            await Task.yield()
        }
    }

    private func repoSource(_ relative: String, filePath: StaticString = #filePath) throws -> String {
        var root = URL(fileURLWithPath: "\(filePath)").deletingLastPathComponent()
        for _ in 0 ..< 16 {
            let candidate = root.appendingPathComponent(relative)
            if FileManager.default.fileExists(atPath: candidate.path) {
                return try String(contentsOf: candidate, encoding: .utf8)
            }
            root.deleteLastPathComponent()
        }
        throw XCTSkip("Could not find \(relative)")
    }
}

@MainActor
private final class RoutePaymentCardServiceStub: PaymentCardServicing {
    var pages: [Result<PaymentCardPage, Error>]
    var cardCursors: [String?] = []
    var updatedBodies: [UpdatePaymentCardBody] = []
    let updatedCard: PaymentCard?

    init(pages: [Result<PaymentCardPage, Error>] = [], updatedCard: PaymentCard? = nil) {
        self.pages = pages
        self.updatedCard = updatedCard
    }

    func cards(after cursor: String?) async throws -> PaymentCardPage {
        cardCursors.append(cursor)
        guard !pages.isEmpty else { return .init(results: []) }
        return try pages.removeFirst().get()
    }

    func searchCardTopics(query _: String) async throws -> [TopicSearchResult] {
        []
    }

    func create(cardId _: String) async throws -> PaymentCard {
        throw RouteTestFailure.unexpected
    }

    func update(id _: String, body: UpdatePaymentCardBody) async throws -> PaymentCard {
        updatedBodies.append(body)
        guard let updatedCard else { throw RouteTestFailure.unexpected }
        return updatedCard
    }

    func delete(id _: String) async throws {}
}

private enum RouteTestFailure: Error {
    case unexpected
}
