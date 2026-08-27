import Foundation
import SwiftUI
import ViewInspector
import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class RewardsProgramStatusesRenderedInteractionTests: XCTestCase {
    func testRenderedEmptyAndListStatesUseNativeControls() throws {
        let empty = RewardsProgramStatusesSurface(
            viewModel: RewardsProgramStatusesViewModel(service: RewardsProgramStatusRenderedServiceStub())
        )
        .environment(\.locale, Locale(identifier: "en_US"))
        XCTAssertNoThrow(try empty.inspect().find(ViewType.TextField.self))
        XCTAssertNoThrow(try empty.inspect().find(button: "Search"))

        let viewModel = RewardsProgramStatusesViewModel(service: RewardsProgramStatusRenderedServiceStub())
        viewModel.statuses = [status(id: "one", name: "Gold", since: "2026-01-02")]
        let list = RewardsProgramStatusesSurface(viewModel: viewModel)
            .environment(\.locale, Locale(identifier: "en_US"))
        XCTAssertNoThrow(try list.inspect().find(text: "Gold"))
        XCTAssertNoThrow(try list.inspect().find(text: "Since: Jan 2, 2026"))
        XCTAssertNoThrow(try list.inspect().find(button: "Edit"))
        XCTAssertNoThrow(try list.inspect().find(button: "Remove"))
    }

    func testRenderedSearchSelectionAddsRepeatedTopicAndGuardsSameRequest() async throws {
        let created = status(id: "created", name: "Gold")
        let service = RewardsProgramStatusRenderedServiceStub(
            searchResults: [topic(id: "topic", name: "Gold")],
            createResult: .success(created),
            suspendCreate: true
        )
        let viewModel = RewardsProgramStatusesViewModel(service: service)
        let sut = RewardsProgramStatusesSurface(viewModel: viewModel)
            .environment(\.locale, Locale(identifier: "en_US"))

        try await ViewHosting.host(sut) {
            try sut.inspect().find(ViewType.TextField.self).setInput("Gold")
            await waitUntil { viewModel.topicQuery == "Gold" }
            try sut.inspect().find(button: "Search").tap()
            await waitUntil { viewModel.topicResults.map(\.id) == ["topic"] }

            try sut.inspect().find(button: "Add").tap()
            await waitUntil { service.createCalls == 1 }
            XCTAssertTrue(try sut.inspect().find(button: "Add").isDisabled())
            XCTAssertThrowsError(try sut.inspect().find(button: "Add").tap())
            service.resumeCreate()
            await waitUntil { viewModel.statuses.map(\.id) == [created.id] }

            try sut.inspect().find(button: "Add").tap()
            await waitUntil { service.createCalls == 2 }
            service.resumeCreate()
            await waitUntil { viewModel.statuses.count == 1 }
        }
    }

    func testRenderedEditOptionalDatesSaveNoOpAndCancel() async throws {
        let original = status(id: "entry", name: "Gold")
        let updated = status(id: "entry", name: "Gold", since: "2026-04-05", until: "2026-06-07")
        let service = RewardsProgramStatusRenderedServiceStub(updateResult: .success(updated))
        let viewModel = RewardsProgramStatusesViewModel(service: service)
        viewModel.statuses = [original]
        let interactionState = RewardsProgramStatusRowInteractionState(status: original)
        let sut = try RewardsProgramStatusRow(
            status: original,
            viewModel: viewModel,
            interactionState: interactionState
        )
        .environment(\.locale, Locale(identifier: "en_US"))
        .environment(\.timeZone, XCTUnwrap(TimeZone(secondsFromGMT: 0)))

        try await ViewHosting.host(sut) {
            try sut.inspect().find(button: "Edit").tap()
            await waitUntil { interactionState.isEditing }
            XCTAssertEqual(try sut.inspect().findAll(ViewType.Toggle.self).count, 2)
            try sut.inspect().findAll(ViewType.Toggle.self)[0].tap()
            try sut.inspect().findAll(ViewType.Toggle.self)[1].tap()
            await waitUntil { interactionState.draft.since != nil && interactionState.draft.until != nil }
            XCTAssertEqual(try sut.inspect().findAll(ViewType.DatePicker.self).count, 2)
            try sut.inspect().find(button: "Save").tap()
            await waitUntil { service.updateCalls == 1 }
            await waitUntil { !interactionState.isEditing }
            XCTAssertEqual(viewModel.statuses.first?.since?.description, "2026-04-05")

            let updatedState = RewardsProgramStatusRowInteractionState(status: updated)
            let updatedRow = RewardsProgramStatusRow(
                status: updated,
                viewModel: viewModel,
                interactionState: updatedState
            )
            .environment(\.locale, Locale(identifier: "en_US"))
            .environment(\.timeZone, TimeZone(secondsFromGMT: 0)!)
            try updatedRow.inspect().find(button: "Edit").tap()
            await waitUntil { updatedState.isEditing }
            try updatedRow.inspect().find(button: "Save").tap()
            await waitUntil { !updatedState.isEditing }
            XCTAssertEqual(service.updateCalls, 1)

            try updatedRow.inspect().find(button: "Edit").tap()
            await waitUntil { updatedState.isEditing }
            try updatedRow.inspect().findAll(ViewType.Toggle.self)[0].tap()
            await waitUntil { updatedState.draft.since == nil }
            try updatedRow.inspect().find(button: "Cancel").tap()
            await waitUntil { !updatedState.isEditing }
            try updatedRow.inspect().find(button: "Edit").tap()
            await waitUntil { updatedState.isEditing }
            XCTAssertEqual(updatedState.draft.since?.description, "2026-04-05")
        }
    }

    func testRenderedDeleteConfirmationCancellationOptimisticRollbackAndDisabledState() async throws {
        let alpha = status(id: "alpha", name: "Alpha")
        let beta = status(id: "beta", name: "Beta")
        let service = RewardsProgramStatusRenderedServiceStub(
            deleteResult: .failure(RenderedStatusError.expected),
            suspendDelete: true
        )
        let viewModel = RewardsProgramStatusesViewModel(service: service)
        viewModel.statuses = [alpha, beta]
        let interactionState = RewardsProgramStatusRowInteractionState(status: beta)
        let sut = RewardsProgramStatusRow(status: beta, viewModel: viewModel, interactionState: interactionState)
            .environment(\.locale, Locale(identifier: "en_US"))

        try await ViewHosting.host(sut) {
            try sut.inspect().find(button: "Remove").tap()
            await waitUntil { interactionState.confirmsDeletion }
            let dialog = try sut.inspect().find(ViewType.VStack.self).confirmationDialog()
            try dialog.actions().find(button: "Cancel").tap()
            await waitUntil { !interactionState.confirmsDeletion }
            XCTAssertTrue(service.deleteIds.isEmpty)

            try sut.inspect().find(button: "Remove").tap()
            await waitUntil { interactionState.confirmsDeletion }
            let confirmation = try sut.inspect().find(ViewType.VStack.self).confirmationDialog()
            try confirmation.actions().find(button: "Confirm").tap()
            await waitUntil { service.deleteIds == [beta.id] }
            await waitUntil { viewModel.statuses.map(\.id) == [alpha.id] }
            XCTAssertTrue(try sut.inspect().find(button: "Remove").isDisabled())

            service.resumeDelete()
            await waitUntil { viewModel.statuses.map(\.id) == [alpha.id, beta.id] }
            XCTAssertNotNil(viewModel.mutationErrorMessage)
        }
    }

    private func status(id: String, name: String, since: String? = nil, until: String? = nil) -> RewardsProgramStatus {
        RewardsProgramStatus(
            id: id,
            rewardsProgramStatusId: "topic-\(id)",
            since: since.flatMap(LocalDate.init),
            until: until.flatMap(LocalDate.init),
            rewardsProgramStatus: .init(id: "topic-\(id)", name: name, slug: name.lowercased())
        )
    }

    private func topic(id: String, name: String) -> TopicSearchResult {
        try! JSONDecoder().decode(
            TopicSearchResult.self,
            from: Data(
                #"{"__entityType":"topic","id":"\#(id)","name":"\#(name)","slug":"gold","topicType":"rewards_program_status"}"#
                    .utf8
            )
        )
    }

    private func waitUntil(_ condition: @escaping @MainActor () -> Bool) async {
        for _ in 0 ..< 500 {
            if condition() {
                return
            }
            await Task.yield()
        }
        XCTFail("Timed out waiting for rendered state")
    }
}

@MainActor
private final class RewardsProgramStatusRenderedServiceStub: RewardsProgramStatusServicing {
    let searchResults: [TopicSearchResult]
    let createResult: Result<RewardsProgramStatus, Error>
    let updateResult: Result<RewardsProgramStatus, Error>
    let deleteResult: Result<Void, Error>
    var suspendCreate: Bool
    var suspendDelete: Bool
    var createCalls = 0
    var updateCalls = 0
    var deleteIds: [String] = []
    private var createContinuations: [CheckedContinuation<Void, Never>] = []
    private var deleteContinuation: CheckedContinuation<Void, Never>?

    init(
        searchResults: [TopicSearchResult] = [],
        createResult: Result<RewardsProgramStatus, Error> = .failure(RenderedStatusError.expected),
        updateResult: Result<RewardsProgramStatus, Error> = .failure(RenderedStatusError.expected),
        deleteResult: Result<Void, Error> = .success(()),
        suspendCreate: Bool = false,
        suspendDelete: Bool = false
    ) {
        self.searchResults = searchResults
        self.createResult = createResult
        self.updateResult = updateResult
        self.deleteResult = deleteResult
        self.suspendCreate = suspendCreate
        self.suspendDelete = suspendDelete
    }

    func statuses(after _: String?) async throws -> RewardsProgramStatusPage {
        .init(results: [])
    }

    func searchStatuses(query _: String) async throws -> [TopicSearchResult] {
        searchResults
    }

    func create(body _: CreateRewardsProgramStatusBody) async throws -> RewardsProgramStatus {
        createCalls += 1
        if suspendCreate {
            await withCheckedContinuation { createContinuations.append($0) }
        }
        return try createResult.get()
    }

    func update(id _: String, body _: UpdateRewardsProgramStatusBody) async throws -> RewardsProgramStatus {
        updateCalls += 1
        return try updateResult.get()
    }

    func delete(id: String) async throws {
        deleteIds.append(id)
        if suspendDelete {
            await withCheckedContinuation { deleteContinuation = $0 }
        }
        try deleteResult.get()
    }

    func resumeCreate() {
        suspendCreate = false
        let continuations = createContinuations
        createContinuations = []
        continuations.forEach { $0.resume() }
    }

    func resumeDelete() {
        suspendDelete = false
        deleteContinuation?.resume()
        deleteContinuation = nil
    }
}

private enum RenderedStatusError: Error { case expected }
