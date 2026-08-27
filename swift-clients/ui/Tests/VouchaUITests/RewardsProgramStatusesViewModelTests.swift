import Foundation
import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class RewardsProgramStatusesViewModelTests: XCTestCase {
    func testCreateAllowsRepeatedTopicsAndNoOpSaveSkipsAPI() async throws {
        let service = RewardsProgramStatusServiceStub()
        let viewModel = RewardsProgramStatusesViewModel(service: service)

        let firstCreated = await viewModel.create(rewardsProgramStatusId: "topic-1")
        let secondCreated = await viewModel.create(rewardsProgramStatusId: "topic-1")
        XCTAssertTrue(firstCreated)
        XCTAssertTrue(secondCreated)
        XCTAssertEqual(service.createCalls, 2)
        let status = try XCTUnwrap(viewModel.statuses.first)
        let saved = await viewModel.save(status: status, draft: RewardsProgramStatusDraft(status: status))
        XCTAssertTrue(saved)
        XCTAssertEqual(service.updateCalls, 0)
    }

    func testReversedDateRangeShowsLocalizedValidationAndSkipsAPI() async throws {
        let service = RewardsProgramStatusServiceStub()
        let viewModel = RewardsProgramStatusesViewModel(service: service)
        let status = status(id: "entry-1")
        viewModel.statuses = [status]
        let draft = try RewardsProgramStatusDraft(
            since: XCTUnwrap(LocalDate("2026-02-01")),
            until: XCTUnwrap(LocalDate("2026-01-01"))
        )

        let saved = await viewModel.save(status: status, draft: draft)

        XCTAssertFalse(saved)
        XCTAssertEqual(service.updateCalls, 0)
        XCTAssertEqual(
            viewModel.mutationErrorMessage,
            .message(.extractedMyRewardsProgramStatusesManagerSinceMustBeBeforeUntilA4745364)
        )
    }

    func testDeleteRollsBackWhenRequestFails() async {
        let service = RewardsProgramStatusServiceStub(deleteError: StatusTestError.failed)
        let viewModel = RewardsProgramStatusesViewModel(service: service)
        viewModel.statuses = [status(id: "entry-1")]

        let deleted = await viewModel.delete(status(id: "entry-1"))
        XCTAssertFalse(deleted)
        XCTAssertEqual(viewModel.statuses.map(\.id), ["entry-1"])
    }

    func testSearchKeepsOnlyRewardsProgramStatusTopics() async {
        let service = RewardsProgramStatusServiceStub(searchResults: [
            topic(id: "match", type: "rewards_program_status"),
            topic(id: "wrong", type: "rewards_program")
        ])
        let viewModel = RewardsProgramStatusesViewModel(service: service)
        viewModel.topicQuery = "Gold"

        await viewModel.searchTopics()
        XCTAssertEqual(viewModel.topicResults.map(\.id), ["match"])
    }

    func testLoadMoreAppendsPreservesRowsOnFailureAndRetries() async {
        let first = status(id: "first")
        let second = status(id: "second")
        let service = RewardsProgramStatusServiceStub(
            pages: [
                .success(.init(results: [first], pageInfo: .init(hasNextPage: true, endCursor: "next"))),
                .failure(StatusTestError.failed),
                .success(.init(results: [second]))
            ]
        )
        let viewModel = RewardsProgramStatusesViewModel(service: service)

        await viewModel.load()
        await viewModel.loadMore()
        XCTAssertEqual(viewModel.statuses.map(\.id), [first.id])
        XCTAssertNotNil(viewModel.continuationErrorMessage)

        await viewModel.loadMore()
        XCTAssertEqual(viewModel.statuses.map(\.id), [first.id, second.id])
        XCTAssertNil(viewModel.continuationErrorMessage)
        XCTAssertEqual(service.cursors, [nil, "next", "next"])
    }

    func testCancellationDoesNotSurfaceLoadOrContinuationErrors() async {
        let first = status(id: "first")
        let second = status(id: "second")
        let service = RewardsProgramStatusServiceStub(pages: [
            .failure(CancellationError()),
            .success(.init(results: [first], pageInfo: .init(hasNextPage: true, endCursor: "next"))),
            .failure(CancellationError()),
            .success(.init(results: [second]))
        ])
        let viewModel = RewardsProgramStatusesViewModel(service: service)

        await viewModel.load()
        XCTAssertNil(viewModel.errorMessage)
        XCTAssertNil(viewModel.continuationErrorMessage)
        await viewModel.load()
        await viewModel.loadMore()
        XCTAssertEqual(viewModel.statuses.map(\.id), [first.id])
        XCTAssertNil(viewModel.continuationErrorMessage)
        await viewModel.loadMore()
        XCTAssertEqual(viewModel.statuses.map(\.id), [first.id, second.id])
    }

    func testCancellationDoesNotSurfaceSearchOrMutationErrors() async throws {
        let status = status(id: "entry-1")
        let service = RewardsProgramStatusServiceStub(
            createError: CancellationError(), updateError: CancellationError(), searchError: CancellationError()
        )
        let viewModel = RewardsProgramStatusesViewModel(service: service)
        viewModel.statuses = [status]
        viewModel.topicQuery = "Gold"

        await viewModel.searchTopics()
        let created = await viewModel.create(rewardsProgramStatusId: "topic-2")
        let saved = try await viewModel.save(
            status: status, draft: RewardsProgramStatusDraft(since: XCTUnwrap(LocalDate("2026-01-02")))
        )

        XCTAssertFalse(created)
        XCTAssertFalse(saved)
        XCTAssertTrue(viewModel.topicResults.isEmpty)
        XCTAssertNil(viewModel.searchErrorMessage)
        XCTAssertNil(viewModel.mutationErrorMessage)
        XCTAssertEqual(viewModel.statuses.map(\.id), [status.id])
    }

    func testCancellationRollsBackAnOptimisticDeleteWithoutShowingAnError() async {
        let status = status(id: "entry-1")
        let service = RewardsProgramStatusServiceStub(deleteError: CancellationError())
        let viewModel = RewardsProgramStatusesViewModel(service: service)
        viewModel.statuses = [status]

        let deleted = await viewModel.delete(status)

        XCTAssertFalse(deleted)
        XCTAssertEqual(viewModel.statuses.map(\.id), [status.id])
        XCTAssertNil(viewModel.mutationErrorMessage)
    }

    func testSameRowMutationIsGuardedWhileDifferentRowsProceedConcurrently() async {
        let alpha = status(id: "alpha")
        let beta = status(id: "beta")
        let service = RewardsProgramStatusServiceStub(
            updatedStatuses: [
                alpha.id: status(id: alpha.id, since: "2026-01-02"),
                beta.id: status(id: beta.id, until: "2026-03-04")
            ],
            suspendUpdates: true
        )
        let viewModel = RewardsProgramStatusesViewModel(service: service)
        viewModel.statuses = [alpha, beta]
        let alphaDraft = RewardsProgramStatusDraft(since: LocalDate("2026-01-02"))
        let betaDraft = RewardsProgramStatusDraft(until: LocalDate("2026-03-04"))

        let first = Task { await viewModel.save(status: alpha, draft: alphaDraft) }
        await waitUntil { service.updateCalls == 1 }
        let duplicate = await viewModel.save(status: alpha, draft: alphaDraft)
        let other = Task { await viewModel.save(status: beta, draft: betaDraft) }
        await waitUntil { service.updateCalls == 2 }
        XCTAssertFalse(duplicate)
        XCTAssertEqual(viewModel.mutatingIds, [alpha.id, beta.id])

        service.resumeUpdates()
        let firstSaved = await first.value
        let otherSaved = await other.value
        XCTAssertTrue(firstSaved)
        XCTAssertTrue(otherSaved)
        XCTAssertTrue(viewModel.mutatingIds.isEmpty)
        XCTAssertEqual(viewModel.statuses.first(where: { $0.id == alpha.id })?.since?.description, "2026-01-02")
        XCTAssertEqual(viewModel.statuses.first(where: { $0.id == beta.id })?.until?.description, "2026-03-04")
    }

    func testStaleRefreshAfterCreatePreservesLocalStatusThenFreshRefreshAcceptsServerRemoval() async {
        let original = status(id: "original")
        let service = RewardsProgramStatusServiceStub()
        let viewModel = RewardsProgramStatusesViewModel(service: service)
        viewModel.statuses = [original]
        service.suspendStatusRequests = true

        let staleRefresh = Task { await viewModel.load() }
        await waitUntil { service.cursors.count == 1 }
        let created = await viewModel.create(rewardsProgramStatusId: "topic-1")
        XCTAssertTrue(created)
        service.resumeNextStatusRequest(with: .success(.init(results: [original])))
        await staleRefresh.value
        XCTAssertEqual(viewModel.statuses.map(\.id), ["entry-1", original.id])

        service.suspendStatusRequests = false
        service.pages = [.success(.init(results: [original]))]
        await viewModel.load()
        XCTAssertEqual(viewModel.statuses.map(\.id), [original.id])
    }

    func testStaleRefreshAfterUpdatePreservesLocalStatusThenFreshRefreshAcceptsServerValue() async {
        let original = status(id: "entry", since: "2026-01-01")
        let local = status(id: original.id, since: "2026-02-01")
        let server = status(id: original.id, since: "2026-03-01")
        let service = RewardsProgramStatusServiceStub(updatedStatuses: [original.id: local])
        let viewModel = RewardsProgramStatusesViewModel(service: service)
        viewModel.statuses = [original]
        service.suspendStatusRequests = true

        let staleRefresh = Task { await viewModel.load() }
        await waitUntil { service.cursors.count == 1 }
        let saved = await viewModel.save(
            status: original, draft: RewardsProgramStatusDraft(since: LocalDate("2026-02-01"))
        )
        XCTAssertTrue(saved)
        service.resumeNextStatusRequest(with: .success(.init(results: [original])))
        await staleRefresh.value
        XCTAssertEqual(viewModel.statuses.first?.since?.description, "2026-02-01")

        service.suspendStatusRequests = false
        service.pages = [.success(.init(results: [server]))]
        await viewModel.load()
        XCTAssertEqual(viewModel.statuses.first?.since?.description, "2026-03-01")
    }

    func testStaleRefreshAfterDeletePreservesLocalRemovalThenFreshRefreshAcceptsServerValue() async {
        let original = status(id: "entry")
        let service = RewardsProgramStatusServiceStub()
        let viewModel = RewardsProgramStatusesViewModel(service: service)
        viewModel.statuses = [original]
        service.suspendStatusRequests = true

        let staleRefresh = Task { await viewModel.load() }
        await waitUntil { service.cursors.count == 1 }
        let deleted = await viewModel.delete(original)
        XCTAssertTrue(deleted)
        service.resumeNextStatusRequest(with: .success(.init(results: [original])))
        await staleRefresh.value
        XCTAssertTrue(viewModel.statuses.isEmpty)

        service.suspendStatusRequests = false
        service.pages = [.success(.init(results: [original]))]
        await viewModel.load()
        XCTAssertEqual(viewModel.statuses.map(\.id), [original.id])
    }

    private func status(id: String, since: String? = nil, until: String? = nil) -> RewardsProgramStatus {
        RewardsProgramStatus(
            id: id,
            rewardsProgramStatusId: "topic-1",
            since: since.flatMap(LocalDate.init),
            until: until.flatMap(LocalDate.init),
            rewardsProgramStatus: .init(id: "topic-1", name: "Gold", slug: "gold")
        )
    }

    private func topic(id: String, type: String) -> TopicSearchResult {
        let data = Data(
            "{\"__entityType\":\"topic\",\"id\":\"\(id)\",\"topicType\":\"\(type)\",\"name\":\"Gold\",\"slug\":\"gold\"}"
                .utf8
        )
        return try! JSONDecoder().decode(TopicSearchResult.self, from: data)
    }

    private func waitUntil(_ condition: @escaping @MainActor () -> Bool) async {
        for _ in 0 ..< 500 {
            if condition() {
                return
            }
            await Task.yield()
        }
        XCTFail("Timed out waiting for status request")
    }
}

private enum StatusTestError: Error { case failed }

@MainActor
private final class RewardsProgramStatusServiceStub: RewardsProgramStatusServicing {
    var createCalls = 0
    var updateCalls = 0
    var createError: Error?
    var updateError: Error?
    var deleteError: Error?
    var searchError: Error?
    let searchResults: [TopicSearchResult]
    var pages: [Result<RewardsProgramStatusPage, Error>]
    var cursors: [String?] = []
    let updatedStatuses: [String: RewardsProgramStatus]
    var suspendUpdates: Bool
    var suspendStatusRequests = false
    private var updateContinuations: [CheckedContinuation<Void, Never>] = []
    private var statusContinuations: [CheckedContinuation<RewardsProgramStatusPage, Error>] = []

    init(
        createError: Error? = nil,
        updateError: Error? = nil,
        deleteError: Error? = nil,
        searchError: Error? = nil,
        searchResults: [TopicSearchResult] = [],
        pages: [Result<RewardsProgramStatusPage, Error>] = [],
        updatedStatuses: [String: RewardsProgramStatus] = [:],
        suspendUpdates: Bool = false
    ) {
        self.createError = createError
        self.updateError = updateError
        self.deleteError = deleteError
        self.searchError = searchError
        self.searchResults = searchResults
        self.pages = pages
        self.updatedStatuses = updatedStatuses
        self.suspendUpdates = suspendUpdates
    }

    func statuses(after cursor: String?) async throws -> RewardsProgramStatusPage {
        cursors.append(cursor)
        if suspendStatusRequests {
            return try await withCheckedThrowingContinuation { statusContinuations.append($0) }
        }
        guard !pages.isEmpty else { return .init(results: []) }
        return try pages.removeFirst().get()
    }

    func searchStatuses(query _: String) async throws -> [TopicSearchResult] {
        if let searchError {
            throw searchError
        }
        return searchResults
    }

    func create(body: CreateRewardsProgramStatusBody) async throws -> RewardsProgramStatus {
        createCalls += 1
        if let createError {
            throw createError
        }
        return RewardsProgramStatus(
            id: "entry-\(createCalls)",
            rewardsProgramStatusId: body.rewardsProgramStatusId,
            rewardsProgramStatus: .init(id: body.rewardsProgramStatusId, name: "Gold", slug: "gold")
        )
    }

    func update(id: String, body _: UpdateRewardsProgramStatusBody) async throws -> RewardsProgramStatus {
        updateCalls += 1
        if let updateError {
            throw updateError
        }
        if suspendUpdates {
            await withCheckedContinuation { updateContinuations.append($0) }
        }
        guard let updated = updatedStatuses[id] else { throw StatusTestError.failed }
        return updated
    }

    func delete(id _: String) async throws {
        if let deleteError {
            throw deleteError
        }
    }

    func resumeUpdates() {
        suspendUpdates = false
        let continuations = updateContinuations
        updateContinuations = []
        continuations.forEach { $0.resume() }
    }

    func resumeNextStatusRequest(with result: Result<RewardsProgramStatusPage, Error>) {
        guard !statusContinuations.isEmpty else {
            XCTFail("No suspended status request to resume")
            return
        }
        statusContinuations.removeFirst().resume(with: result)
    }
}
