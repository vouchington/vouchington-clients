import Foundation
import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class PointValuationsViewModelTests: XCTestCase {
    func testLoadsPagesAndPreservesLocalMutations() async {
        let first = valuation(id: "a", name: "Alpha", value: 1)
        let second = valuation(id: "b", name: "Beta", value: 2)
        let service = PointValuationServiceStub(
            pages: [
                .init(results: [first], pageInfo: .init(hasNextPage: true, endCursor: "next")),
                .init(results: [first, second])
            ],
            created: valuation(id: "c", name: "Created", value: 3)
        )
        let viewModel = PointValuationsViewModel(service: service)

        await viewModel.load()
        let created = await viewModel.create(rewardsProgramId: "created", draft: draft("3"))
        XCTAssertTrue(created)
        await viewModel.loadMore()

        XCTAssertEqual(viewModel.valuations.map(\.rewardsProgram.name), ["Alpha", "Beta", "Created"])
        XCTAssertEqual(Set(viewModel.valuations.map(\.id)).count, 3)
        XCTAssertEqual(service.cursors, [nil, "next"])
    }

    func testSearchRejectsStaleAndWrongTypeResults() async throws {
        let service = try PointValuationServiceStub(searchResults: topicResults())
        service.shouldDelay = true
        let viewModel = PointValuationsViewModel(service: service)
        viewModel.topicQuery = "Old"

        let staleSearch = Task { await viewModel.searchTopics() }
        await Task.yield()
        viewModel.topicQuery = "New"
        await staleSearch.value

        XCTAssertTrue(viewModel.topicResults.isEmpty)
        service.shouldDelay = false
        await viewModel.searchTopics()
        XCTAssertEqual(viewModel.topicResults.map(\.name), ["Valid Rewards"])
    }

    func testCreateAndUpdateGuardsAndNoOpSave() async {
        let original = valuation(id: "a", name: "Alpha", value: 1)
        let updated = valuation(id: "a", name: "Alpha", value: 2)
        let service = PointValuationServiceStub(created: original, updated: updated)
        service.shouldDelay = true
        let viewModel = PointValuationsViewModel(service: service)
        viewModel.valuations = [original]

        async let createA = viewModel.create(rewardsProgramId: "alpha", draft: draft("1"))
        async let createB = viewModel.create(rewardsProgramId: "alpha", draft: draft("1"))
        let createResults = await [createA, createB]
        XCTAssertEqual(service.createCalls, 1)
        XCTAssertEqual(createResults.filter { $0 }.count, 1)

        let noOpSaved = await viewModel.save(
            valuation: original,
            draft: PointValuationDraft(valuation: original)
        )
        XCTAssertTrue(noOpSaved)
        XCTAssertEqual(service.updateCalls, 0)

        let duplicateCreated = await viewModel.create(
            rewardsProgramId: original.rewardsProgramId,
            draft: draft("1")
        )
        XCTAssertFalse(duplicateCreated)
        XCTAssertEqual(service.createCalls, 1)

        var changed = PointValuationDraft(valuation: original)
        changed.valuePerPointText = "2"
        let changedDraft = changed
        async let updateA = viewModel.save(valuation: original, draft: changedDraft)
        async let updateB = viewModel.save(valuation: original, draft: changedDraft)
        let updateResults = await [updateA, updateB]
        XCTAssertEqual(service.updateCalls, 1)
        XCTAssertEqual(updateResults.filter { $0 }.count, 1)
    }

    func testDeleteIsOptimisticAndRollsBackAtOriginalRank() async {
        let alpha = valuation(id: "a", name: "Alpha", value: 1)
        let beta = valuation(id: "b", name: "Beta", value: 2)
        let gamma = valuation(id: "c", name: "Gamma", value: 3)
        let service = PointValuationServiceStub()
        service.deleteResult = .failure(PointValuationTestError.expected)
        service.shouldDelay = true
        let viewModel = PointValuationsViewModel(service: service)
        viewModel.valuations = [alpha, beta, gamma]

        let deletion = Task { await viewModel.delete(beta) }
        await waitUntil { service.deleteCalls == 1 }
        XCTAssertEqual(viewModel.valuations.map(\.id), ["a", "c"])
        let deleted = await deletion.value

        XCTAssertFalse(deleted)
        XCTAssertEqual(viewModel.valuations.map(\.id), ["a", "b", "c"])
        XCTAssertNotNil(viewModel.mutationErrorMessage)
    }

    func testFailedDeleteRestoresLocalUpdateAcrossStaleRefresh() async {
        let original = valuation(id: "a", name: "Alpha", value: 1)
        let locallyUpdated = valuation(id: "a", name: "Alpha", value: 2)
        let stalePage = PointValuationPage(results: [original])
        let service = PointValuationServiceStub(
            pages: [stalePage, stalePage],
            updated: locallyUpdated
        )
        service.deleteResult = .failure(PointValuationTestError.expected)
        let viewModel = PointValuationsViewModel(service: service)
        await viewModel.load()
        var changed = PointValuationDraft(valuation: original)
        changed.valuePerPointText = "2"

        let saved = await viewModel.save(valuation: original, draft: changed)
        let deleted = await viewModel.delete(locallyUpdated)
        await viewModel.load()

        XCTAssertTrue(saved)
        XCTAssertFalse(deleted)
        XCTAssertEqual(viewModel.valuations.first?.valuePerPoint.amount, 2_000_000)
    }

    private func valuation(id: String, name: String, value: Decimal) -> PointValuation {
        PointValuation(
            id: id,
            rewardsProgramId: "program-\(id)",
            valuePerPoint: try! ScaledMoney(
                amount: NSDecimalNumber(decimal: value * 1_000_000).int64Value,
                currency: "usd"
            ),
            note: nil,
            rewardsProgram: .init(id: "program-\(id)", name: name, slug: name.lowercased())
        )
    }

    private func draft(_ value: String) -> PointValuationDraft {
        var draft = PointValuationDraft(locale: Locale(identifier: "en_US"))
        draft.valuePerPointText = value
        return draft
    }

    private func topicResults() throws -> [TopicSearchResult] {
        try JSONDecoder().decode(
            [TopicSearchResult].self,
            from: Data(
                #"[{"__entityType":"topic","id":"valid","name":"Valid Rewards","slug":"valid","topicType":"rewards_program"},{"__entityType":"topic","id":"wrong","name":"Wrong Type","slug":"wrong","topicType":"card"},{"__entityType":"topic","id":"missing","name":null,"slug":"missing","topicType":"rewards_program"}]"#
                    .utf8
            )
        )
    }

    private func waitUntil(_ condition: @escaping @MainActor () -> Bool) async {
        for _ in 0 ..< 100 where !condition() {
            await Task.yield()
        }
    }
}

@MainActor
private final class PointValuationServiceStub: PointValuationServicing {
    var pages: [PointValuationPage]
    var searchResults: [TopicSearchResult]
    var created: PointValuation?
    var updated: PointValuation?
    var deleteResult: Result<Void, Error> = .success(())
    var shouldDelay = false
    var cursors: [String?] = []
    var createCalls = 0
    var updateCalls = 0
    var deleteCalls = 0

    init(
        pages: [PointValuationPage] = [],
        searchResults: [TopicSearchResult] = [],
        created: PointValuation? = nil,
        updated: PointValuation? = nil
    ) {
        self.pages = pages
        self.searchResults = searchResults
        self.created = created
        self.updated = updated
    }

    func valuations(after: String?) async throws -> PointValuationPage {
        cursors.append(after)
        guard !pages.isEmpty else { return .init(results: []) }
        return pages.removeFirst()
    }

    func searchRewardsPrograms(query _: String) async throws -> [TopicSearchResult] {
        await delayIfNeeded()
        return searchResults
    }

    func create(body _: CreatePointValuationBody) async throws -> PointValuation {
        createCalls += 1
        await delayIfNeeded()
        guard let created else { throw PointValuationTestError.expected }
        return created
    }

    func update(id _: String, body _: UpdatePointValuationBody) async throws -> PointValuation {
        updateCalls += 1
        await delayIfNeeded()
        guard let updated else { throw PointValuationTestError.expected }
        return updated
    }

    func delete(id _: String) async throws {
        deleteCalls += 1
        await delayIfNeeded()
        try deleteResult.get()
    }

    private func delayIfNeeded() async {
        guard shouldDelay else { return }
        for _ in 0 ..< 20 {
            await Task.yield()
        }
    }
}

private enum PointValuationTestError: Error {
    case expected
}
