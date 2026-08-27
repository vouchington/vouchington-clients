import Foundation
import SwiftUI
import ViewInspector
import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class SpendingCategoriesViewModelTests: XCTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.contentTypes = [:]
        CannedFeedURLProtocol.errors = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
        CannedFeedURLProtocol.discardPendingResponses()
    }

    override func tearDown() {
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.contentTypes = [:]
        CannedFeedURLProtocol.errors = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
        CannedFeedURLProtocol.discardPendingResponses()
        super.tearDown()
    }

    func testPaginationDedupesAndSearchGenerationRejectsStaleSuccessAndFailure() async {
        let service = SpendingServiceStub(pages: [
            .success(page([category("a")], more: true, cursor: "next")),
            .success(page([category("a", amount: 200), category("b")]))
        ])
        let viewModel = SpendingCategoriesViewModel(service: service)
        await viewModel.load()
        await viewModel.loadMore()
        XCTAssertEqual(viewModel.categories.map(\.id), ["a", "b"])
        XCTAssertEqual(viewModel.pagination.items.map(\.id), ["a", "b"])
        XCTAssertEqual(viewModel.categories.first?.amount, money(100))
        service.searchDelay = true
        viewModel.topicQuery = "old"
        let stale = Task { await viewModel.searchTopics() }
        await waitUntil { service.searchCalls == 1 }
        viewModel.topicQuery = "new"
        service.searchResult = .failure(SpendingCategoryTestError.expected)
        service.resumeSearch()
        await stale.value
        XCTAssertTrue(viewModel.topicResults.isEmpty)
        XCTAssertNil(viewModel.searchErrorMessage)
    }

    func testCreateAllowsDuplicateTopicButGuardsInflightAndSaveKeepsInvalidDraft() async {
        let original = category("a", topic: "shared")
        let created = category("b", topic: "shared")
        let service = SpendingServiceStub(created: created)
        service.createDelay = true
        let viewModel = SpendingCategoriesViewModel(service: service)
        viewModel.categories = [original]
        var create = SpendingCategoryDraft(locale: Locale(identifier: "en_US"))
        create.updateAmountText("2")
        let createDraft = create
        async let first = viewModel.create(spendingCategoryId: "shared", draft: createDraft)
        async let second = viewModel.create(spendingCategoryId: "shared", draft: createDraft)
        await waitUntil { service.createCalls == 1 }
        service.resumeCreate()
        let firstResult = await first
        let secondResult = await second
        XCTAssertEqual([firstResult, secondResult].filter { $0 }.count, 1)
        XCTAssertEqual(viewModel.categories.map(\.id), ["a", "b"])

        var invalid = SpendingCategoryDraft(category: original, locale: Locale(identifier: "en_US"))
        invalid.updateAmountText("not a decimal")
        let savedInvalid = await viewModel.save(category: original, draft: invalid)
        XCTAssertFalse(savedInvalid)
        XCTAssertEqual(service.updateCalls, 0)
        XCTAssertNotNil(viewModel.mutationErrorMessage)
    }

    func testEmptyNoteClearsNoOpAvoidsRequestAndDeleteRollsBack() async {
        let original = category("a", note: "old")
        let updated = category("a", note: nil)
        let service = SpendingServiceStub(updated: updated)
        let viewModel = SpendingCategoriesViewModel(service: service)
        viewModel.categories = [original, category("b")]
        var draft = SpendingCategoryDraft(category: original, locale: Locale(identifier: "en_US"))
        draft.note = ""
        let saved = await viewModel.save(category: original, draft: draft)
        XCTAssertTrue(saved)
        XCTAssertEqual(service.updateCalls, 1)
        XCTAssertNil(viewModel.categories.first?.note)
        let noOpSaved = await viewModel.save(category: updated, draft: SpendingCategoryDraft(category: updated))
        XCTAssertTrue(noOpSaved)
        XCTAssertEqual(service.updateCalls, 1)

        service.deleteResult = .failure(SpendingCategoryTestError.expected)
        let deleted = await viewModel.delete(updated)
        XCTAssertFalse(deleted)
        XCTAssertEqual(viewModel.categories.map(\.id), ["a", "b"])
    }

    func testReadOnlyRowsSuppressMutationsAndLocaleKeepsEditedText() async {
        let member = category("member", canManage: false)
        let service = SpendingServiceStub()
        let viewModel = SpendingCategoriesViewModel(service: service)
        viewModel.categories = [member]
        let deleted = await viewModel.delete(member)
        XCTAssertFalse(deleted)
        var draft = SpendingCategoryDraft(amount: money(150), locale: Locale(identifier: "en_US"))
        draft.applyLocale(Locale(identifier: "fr_FR"))
        XCTAssertEqual(draft.amountText, "1,5")
        draft.updateAmountText("1,75")
        draft.applyLocale(Locale(identifier: "en_US"))
        XCTAssertEqual(draft.amountText, "1,75")
    }

    func testLoadAndContinuationFailuresExposeRetryState() async {
        let service = SpendingServiceStub(pages: [.failure(SpendingCategoryTestError.expected)])
        let viewModel = SpendingCategoriesViewModel(service: service)

        await viewModel.load()
        XCTAssertNotNil(viewModel.errorMessage)
        XCTAssertFalse(viewModel.isLoading)

        service.pages = [
            .success(page([category("a")], more: true, cursor: "next")),
            .failure(SpendingCategoryTestError.expected)
        ]
        await viewModel.load()
        await viewModel.loadMore()
        XCTAssertNotNil(viewModel.continuationErrorMessage)
        XCTAssertFalse(viewModel.isLoadingMore)
    }

    func testSearchFiltersUnnamedAndDuplicateResultsAndClearsBlankQuery() async throws {
        let service = SpendingServiceStub()
        service.searchResult = try .success([
            topicResult(id: "first", name: "First", slug: "first"),
            topicResult(id: "first", name: "Duplicate", slug: "duplicate"),
            topicResult(id: "unnamed", name: nil, slug: "unnamed")
        ])
        let viewModel = SpendingCategoriesViewModel(service: service)

        viewModel.topicQuery = "  category "
        await viewModel.searchTopics()
        XCTAssertEqual(viewModel.topicResults.map(\.id), ["first"])

        viewModel.topicQuery = "   "
        await viewModel.searchTopics()
        XCTAssertTrue(viewModel.topicResults.isEmpty)
        XCTAssertNil(viewModel.searchErrorMessage)
    }

    func testCreateSaveAndDeleteFailuresPreserveStateAndReportMutationErrors() async {
        let existing = category("existing")
        let service = SpendingServiceStub()
        let viewModel = SpendingCategoriesViewModel(service: service)
        viewModel.categories = [existing]
        var valid = SpendingCategoryDraft(amount: money(1_000), locale: Locale(identifier: "en_US"))

        let created = await viewModel.create(spendingCategoryId: "topic", draft: valid)
        XCTAssertFalse(created)
        XCTAssertNotNil(viewModel.mutationErrorMessage)

        valid.updateAmountText("11")
        let saved = await viewModel.save(category: existing, draft: valid)
        XCTAssertFalse(saved)
        XCTAssertNotNil(viewModel.mutationErrorMessage)

        service.deleteResult = .failure(SpendingCategoryTestError.expected)
        let deleted = await viewModel.delete(existing)
        XCTAssertFalse(deleted)
        XCTAssertEqual(viewModel.categories.map(\.id), ["existing"])
    }

    func testSurfaceAndEditorRenderCreationPaginationAndEditableControls() throws {
        let editable = category("editable", amount: 350, note: "Note")
        let viewModel = SpendingCategoriesViewModel(service: SpendingServiceStub())
        viewModel.categories = [editable]
        viewModel.pagination.reset(items: [editable])
        let surface = SpendingCategoriesSurface(viewModel: viewModel, locale: Locale(identifier: "en_US"))
        let inspected = try surface.inspect()

        XCTAssertNoThrow(try inspected.find(button: "Search"))
        XCTAssertNoThrow(try inspected.find(button: "Edit"))
        XCTAssertNoThrow(try inspected.find(button: "Remove"))
        XCTAssertNoThrow(try inspected.find(viewWithAccessibilityIdentifier: "spending-categories-pagination"))

        var draft = SpendingCategoryDraft(
            amount: money(200),
            note: "Optional",
            locale: Locale(identifier: "en_US")
        )
        let editor = SpendingCategoryEditor(
            draft: Binding(get: { draft }, set: { draft = $0 }),
            isSaving: true,
            save: {},
            cancel: {}
        )
        let editorInspection = try editor.inspect()
        XCTAssertEqual(editorInspection.findAll(ViewType.TextField.self).count, 2)
        XCTAssertEqual(editorInspection.findAll(ViewType.Picker.self).count, 2)
        XCTAssertTrue(try editorInspection.find(button: "Save").isDisabled())
        XCTAssertTrue(try editorInspection.find(button: "Cancel").isDisabled())
    }

    func testSurfaceRendersTopicResultAndInitialLoadingAndErrorStates() throws {
        let service = SpendingServiceStub()
        let viewModel = SpendingCategoriesViewModel(service: service)
        viewModel.topicResults = try [topicResult(id: "topic", name: "Groceries", slug: "groceries")]
        viewModel.searchErrorMessage = .verbatim("Search failed")
        viewModel.errorMessage = .verbatim("Load failed")
        let loaded = SpendingCategoriesSurface(viewModel: viewModel, locale: Locale(identifier: "en_US"))
        let loadedInspection = try loaded.inspect()

        XCTAssertNoThrow(try loadedInspection.find(text: "Groceries"))
        XCTAssertNoThrow(try loadedInspection.find(text: "Search failed"))
        XCTAssertNoThrow(try loadedInspection.find(text: "Load failed"))
        XCTAssertGreaterThan(loadedInspection.findAll(ViewType.Button.self).count, 0)
        XCTAssertThrowsError(try loadedInspection
            .find(viewWithAccessibilityIdentifier: "spending-categories-pagination"))

        let loadingViewModel = SpendingCategoriesViewModel(service: service)
        loadingViewModel.isLoading = true
        let loading = SpendingCategoriesSurface(viewModel: loadingViewModel, locale: Locale(identifier: "en_US"))
        XCTAssertNoThrow(try loading.inspect().find(ViewType.ProgressView.self))
    }

    func testSignedOutRoutePresentsNativeSignInAction() throws {
        var signInCalls = 0
        let route = SpendingCategoriesRouteSurface(client: nil, isSignedIn: false) { signInCalls += 1 }
        let inspected = try route.inspect()

        XCTAssertNoThrow(try inspected.find(text: "Sign in required"))
        try inspected.find(button: "Sign in").tap()
        XCTAssertEqual(signInCalls, 1)
    }

    func testNativeRouteDestinationUsesDedicatedSpendingCategoriesSurface() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/spending-categories"))
        let destination = NativeRouteDestinationView(
            entry: route.entry,
            routeMatch: route.match,
            isSignedIn: false
        )

        XCTAssertNoThrow(try destination.inspect().find(text: "Sign in required"))
    }

    func testRenderedRowEditCancellationRestoresEditState() async throws {
        let original = category("entry", amount: 350, note: "Original")
        let service = SpendingServiceStub()
        let viewModel = SpendingCategoriesViewModel(service: service)
        viewModel.categories = [original]
        let interactionState = SpendingCategoryRowInteractionState(category: original)
        let row = SpendingCategoryRow(category: original, viewModel: viewModel, interactionState: interactionState)
            .environment(\.locale, Locale(identifier: "en_US"))

        try await ViewHosting.host(row) {
            try row.inspect().find(button: "Edit").tap()
            await waitUntil { interactionState.isEditing }
            try row.inspect().find(ViewType.TextField.self).setInput("99")
            await waitUntil { interactionState.draft.amountText == "99" }
            try row.inspect().find(button: "Cancel").tap()
            await waitUntil { !interactionState.isEditing }
            XCTAssertEqual(interactionState.draft.amountText, "3.5")
        }
    }

    func testRenderedRowRemovalCancellationAndConfirmationRespectUserIntent() async throws {
        let original = category("entry", amount: 350, note: "Original")
        let service = SpendingServiceStub()
        let viewModel = SpendingCategoriesViewModel(service: service)
        viewModel.categories = [original]
        let interactionState = SpendingCategoryRowInteractionState(category: original)
        let row = SpendingCategoryRow(category: original, viewModel: viewModel, interactionState: interactionState)
            .environment(\.locale, Locale(identifier: "en_US"))

        try await ViewHosting.host(row) {
            try row.inspect().find(button: "Remove").tap()
            await waitUntil { interactionState.confirmsDeletion }
            let dialog = try row.inspect().find(ViewType.VStack.self).confirmationDialog()
            try dialog.actions().find(button: "Cancel").tap()
            await waitUntil { !interactionState.confirmsDeletion }
            XCTAssertEqual(service.deleteCalls, 0)
            XCTAssertEqual(viewModel.categories.map(\.id), [original.id])

            try row.inspect().find(button: "Remove").tap()
            await waitUntil { interactionState.confirmsDeletion }
            let confirmedDialog = try row.inspect().find(ViewType.VStack.self).confirmationDialog()
            try confirmedDialog.actions().find(button: "Confirm").tap()
            await waitUntil { service.deleteCalls == 1 }
            XCTAssertTrue(viewModel.categories.isEmpty)
        }
    }

    func testServiceUsesNativeReadSearchAndMutationEndpoints() async throws {
        let categoryJSON = """
        {"id":"entry","spending_category_id":"topic","amount":{"amount":350,"currency":"usd"},"spending_frequency":"monthly","note":null,"owner_type":"individual","can_manage":true,"spending_category":{"id":"topic","name":"Groceries","slug":"groceries"}}
        """
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/spending-categories"] = [
            (Data("{\"results\":[\(categoryJSON)],\"page_info\":{\"has_next_page\":false}}".utf8), 200, 0),
            (Data("{\"spending_category\":\(categoryJSON)}".utf8), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/topics"] = (
            Data(
                "{\"results\":[{\"id\":\"topic\",\"name\":\"Groceries\",\"slug\":\"groceries\",\"__entity_type\":\"topic\"}],\"page_info\":{\"has_next_page\":false},\"topics\":{}}"
                    .utf8
            ), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/spending-categories/entry"] = (
            Data("{\"spending_category\":\(categoryJSON)}".utf8), 200
        )
        let service = SpendingCategoryService(client: makeClient())
        let create = CreateSpendingCategoryBody(
            spendingCategoryId: "topic", amount: money(350), spendingFrequency: .monthly
        )

        let page = try await service.categories(after: "cursor")
        let topics = try await service.searchTopics(query: "gro")
        let created = try await service.create(body: create)
        let updated = try await service.update(id: "entry", body: .init(note: .null))
        XCTAssertEqual(page.results.map(\.id), ["entry"])
        XCTAssertEqual(topics.map(\.id), ["topic"])
        XCTAssertEqual(created.id, "entry")
        XCTAssertEqual(updated.id, "entry")
        try await service.delete(id: "entry")

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["GET", "GET", "POST", "PATCH", "DELETE"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/my/spending-categories", "/api/v1/topics", "/api/v1/my/spending-categories",
            "/api/v1/my/spending-categories/entry", "/api/v1/my/spending-categories/entry"
        ])
    }

    private func category(
        _ id: String,
        topic: String? = nil,
        amount: Int64 = 100,
        currency: String = "usd",
        note: String? = nil,
        canManage: Bool = true
    ) -> SpendingCategory {
        .init(
            id: id,
            spendingCategoryId: topic ?? "topic-\(id)",
            amount: money(amount, currency: currency),
            spendingFrequency: .monthly,
            note: note,
            ownerType: canManage ? .individual : .household,
            canManage: canManage,
            spendingCategory: .init(id: topic ?? "topic-\(id)", name: id, slug: id)
        )
    }

    private func money(_ amount: Int64, currency: String = "usd") -> Money {
        try! Money(amount: amount, currency: currency)
    }

    private func page(
        _ results: [SpendingCategory],
        more: Bool = false,
        cursor: String? = nil
    ) -> SpendingCategoryPage {
        .init(
            results: results,
            pageInfo: .init(hasNextPage: more, endCursor: cursor)
        )
    }

    private func waitUntil(_ condition: @escaping @MainActor () -> Bool) async {
        for _ in 0 ..< 200 where !condition() {
            await Task.yield()
        }
    }

    private func jsonObject(_ value: some Encodable) throws -> [String: Any] {
        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        return try XCTUnwrap(JSONSerialization.jsonObject(with: encoder.encode(value)) as? [String: Any])
    }

    private func makeClient() -> APIClient {
        APIClient(
            config: AppConfig(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test-site-key"),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
    }

    private func topicResult(id: String, name: String?, slug: String) throws -> TopicSearchResult {
        let encodedName = name.map { "\"\($0)\"" } ?? "null"
        return try JSONDecoder().decode(
            TopicSearchResult.self,
            from: Data(
                "{\"id\":\"\(id)\",\"name\":\(encodedName),\"slug\":\"\(slug)\",\"__entityType\":\"topic\"}".utf8
            )
        )
    }
}
