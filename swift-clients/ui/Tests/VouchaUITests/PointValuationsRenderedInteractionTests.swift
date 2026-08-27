import Foundation
import SwiftUI
import ViewInspector
import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class PointValuationsRenderedInteractionTests: XCTestCase {
    func testRenderedAddResetsDraftAndPreventsDuplicate() async throws {
        let created = valuation(id: "created", programId: "program-1", name: "Example Rewards", value: 2)
        let service = RenderedPointValuationServiceStub(createResult: .success(created))
        let viewModel = PointValuationsViewModel(service: service)
        viewModel.topicResults = try [topic(id: "program-1", name: "Example Rewards")]
        let interactionState = PointValuationsSurfaceInteractionState()
        let sut = PointValuationsSurface(viewModel: viewModel, interactionState: interactionState)
            .environment(\.locale, Locale(identifier: "en_US"))

        try await ViewHosting.host(sut) {
            let fields = try sut.inspect().findAll(ViewType.TextField.self)
            try fields[0].setInput("2")
            try fields[1].setInput("Everyday redemption")
            await waitUntil { interactionState.createDraft.valuePerPointText == "2" }
            try sut.inspect().find(button: "Add").tap()
            await waitUntil { service.createCalls == 1 }
            await waitUntil { viewModel.valuations.map(\.id) == [created.id] }

            XCTAssertEqual(interactionState.createDraft.valuePerPointText, "")
            XCTAssertEqual(interactionState.createDraft.note, "")
            await waitUntil { (try? sut.inspect().find(button: "Add").isDisabled()) == true }
            XCTAssertTrue(try sut.inspect().find(button: "Add").isDisabled())

            XCTAssertThrowsError(try sut.inspect().find(button: "Add").tap())
            await Task.yield()
            XCTAssertEqual(service.createCalls, 1)
        }
    }

    func testRenderedNoOpSaveClosesEditorWithoutRequest() async throws {
        let original = valuation(id: "a", programId: "program-a", name: "Alpha", value: 1)
        let service = RenderedPointValuationServiceStub()
        let viewModel = PointValuationsViewModel(service: service)
        viewModel.valuations = [original]
        let interactionState = PointValuationRowInteractionState(valuation: original)
        let sut = PointValuationRow(
            valuation: original,
            viewModel: viewModel,
            interactionState: interactionState
        )
        .environment(\.locale, Locale(identifier: "en_US"))

        try await ViewHosting.host(sut) {
            try sut.inspect().find(button: "Edit").tap()
            await waitUntil { interactionState.isEditing }
            XCTAssertNoThrow(try sut.inspect().find(button: "Save"))

            try sut.inspect().find(button: "Save").tap()
            await waitUntil { !interactionState.isEditing }

            XCTAssertEqual(service.updateCalls, 0)
            XCTAssertThrowsError(try sut.inspect().find(button: "Save"))
        }
    }

    func testRenderedEditSaveAndFailedSavePreserveDraft() async throws {
        let original = valuation(id: "a", programId: "program-a", name: "Alpha", value: 1)
        let updated = valuation(id: "a", programId: "program-a", name: "Alpha", value: 2)
        let successService = RenderedPointValuationServiceStub(updateResult: .success(updated))
        let successViewModel = PointValuationsViewModel(service: successService)
        successViewModel.valuations = [original]
        let successState = PointValuationRowInteractionState(valuation: original)
        let successSut = PointValuationRow(
            valuation: original,
            viewModel: successViewModel,
            interactionState: successState
        )
        .environment(\.locale, Locale(identifier: "en_US"))

        try await ViewHosting.host(successSut) {
            try successSut.inspect().find(button: "Edit").tap()
            await waitUntil { successState.isEditing }
            try successSut.inspect().find(ViewType.TextField.self).setInput("2")
            await waitUntil { successState.draft.valuePerPointText == "2" }
            try successSut.inspect().find(button: "Save").tap()
            await waitUntil { successService.updateCalls == 1 }
            await waitUntil { !successState.isEditing }

            XCTAssertEqual(successViewModel.valuations.first?.valuePerPoint.amount, 2_000_000)
        }

        let failureService = RenderedPointValuationServiceStub(
            updateResult: .failure(RenderedPointValuationError.expected)
        )
        let failureViewModel = PointValuationsViewModel(service: failureService)
        failureViewModel.valuations = [original]
        let failureState = PointValuationRowInteractionState(valuation: original)
        let failureSut = PointValuationRow(
            valuation: original,
            viewModel: failureViewModel,
            interactionState: failureState
        )
        .environment(\.locale, Locale(identifier: "en_US"))

        try await ViewHosting.host(failureSut) {
            try failureSut.inspect().find(button: "Edit").tap()
            await waitUntil { failureState.isEditing }
            try failureSut.inspect().find(ViewType.TextField.self).setInput("7.25")
            await waitUntil { failureState.draft.valuePerPointText == "7.25" }
            try failureSut.inspect().find(button: "Save").tap()
            await waitUntil { failureService.updateCalls == 1 }
            await waitUntil { failureViewModel.mutationErrorMessage != nil }

            XCTAssertEqual(
                failureState.draft.valuePerPointText,
                "7.25"
            )
            XCTAssertNoThrow(try failureSut.inspect().find(button: "Save"))
        }
    }

    func testRenderedCancelDiscardsDraftChanges() async throws {
        let original = valuation(id: "a", programId: "program-a", name: "Alpha", value: 1)
        let viewModel = PointValuationsViewModel(service: RenderedPointValuationServiceStub())
        viewModel.valuations = [original]
        let interactionState = PointValuationRowInteractionState(valuation: original)
        let sut = PointValuationRow(
            valuation: original,
            viewModel: viewModel,
            interactionState: interactionState
        )
        .environment(\.locale, Locale(identifier: "en_US"))

        try await ViewHosting.host(sut) {
            try sut.inspect().find(button: "Edit").tap()
            await waitUntil { interactionState.isEditing }
            try sut.inspect().find(ViewType.TextField.self).setInput("99")
            await waitUntil { interactionState.draft.valuePerPointText == "99" }
            try sut.inspect().find(button: "Cancel").tap()
            await waitUntil { !interactionState.isEditing }
            try sut.inspect().find(button: "Edit").tap()
            await waitUntil { interactionState.isEditing }

            XCTAssertEqual(interactionState.draft.valuePerPointText, "1")
        }
    }

    func testRenderedRemoveCancellationDoesNotDelete() async throws {
        let original = valuation(id: "a", programId: "program-a", name: "Alpha", value: 1)
        let service = RenderedPointValuationServiceStub()
        let viewModel = PointValuationsViewModel(service: service)
        viewModel.valuations = [original]
        let interactionState = PointValuationRowInteractionState(valuation: original)
        let sut = PointValuationRow(
            valuation: original,
            viewModel: viewModel,
            interactionState: interactionState
        )
        .environment(\.locale, Locale(identifier: "en_US"))

        try await ViewHosting.host(sut) {
            try sut.inspect().find(button: "Remove").tap()
            await waitUntil { interactionState.confirmsDeletion }
            let dialog = try sut.inspect().find(ViewType.VStack.self).confirmationDialog()
            XCTAssertEqual(try dialog.title().string(), "Remove?")
            XCTAssertNoThrow(try dialog.actions().find(button: "Confirm"))

            try dialog.actions().find(button: "Cancel").tap()
            await waitUntil { !interactionState.confirmsDeletion }

            XCTAssertTrue(service.deleteIds.isEmpty)
            XCTAssertEqual(viewModel.valuations.map(\.id), [original.id])
            XCTAssertThrowsError(
                try sut.inspect().find(ViewType.VStack.self).confirmationDialog()
            )
        }
    }

    func testRenderedRemoveIsOptimisticAndRollsBack() async throws {
        let alpha = valuation(id: "a", programId: "program-a", name: "Alpha", value: 1)
        let beta = valuation(id: "b", programId: "program-b", name: "Beta", value: 2)
        let gamma = valuation(id: "c", programId: "program-c", name: "Gamma", value: 3)
        let service = RenderedPointValuationServiceStub(
            deleteResult: .failure(RenderedPointValuationError.expected),
            suspendDelete: true
        )
        let viewModel = PointValuationsViewModel(service: service)
        viewModel.valuations = [alpha, beta, gamma]
        let interactionState = PointValuationRowInteractionState(valuation: beta)
        let sut = PointValuationRow(
            valuation: beta,
            viewModel: viewModel,
            interactionState: interactionState
        )
        .environment(\.locale, Locale(identifier: "en_US"))

        try await ViewHosting.host(sut) {
            try sut.inspect().find(button: "Remove").tap()
            await waitUntil { interactionState.confirmsDeletion }
            let dialog = try sut.inspect().find(ViewType.VStack.self).confirmationDialog()
            try dialog.actions().find(button: "Confirm").tap()
            await waitUntil { service.deleteIds == [beta.id] }
            await waitUntil { viewModel.valuations.map(\.id) == [alpha.id, gamma.id] }

            let optimisticSurface = PointValuationsSurface(viewModel: viewModel)
            XCTAssertThrowsError(try optimisticSurface.inspect().find(text: "Beta"))
            XCTAssertNoThrow(try optimisticSurface.inspect().find(text: "Alpha"))
            XCTAssertNoThrow(try optimisticSurface.inspect().find(text: "Gamma"))

            service.resumeDelete()
            await waitUntil { viewModel.valuations.map(\.id) == [alpha.id, beta.id, gamma.id] }

            let rolledBackSurface = PointValuationsSurface(viewModel: viewModel)
            XCTAssertNoThrow(try rolledBackSurface.inspect().find(text: "Beta"))
            XCTAssertNotNil(viewModel.mutationErrorMessage)
        }
    }

    private func valuation(
        id: String,
        programId: String,
        name: String,
        value: Decimal
    ) -> PointValuation {
        PointValuation(
            id: id,
            rewardsProgramId: programId,
            valuePerPoint: try! ScaledMoney(
                amount: NSDecimalNumber(decimal: value * 1_000_000).int64Value,
                currency: "usd"
            ),
            note: nil,
            rewardsProgram: .init(id: programId, name: name, slug: name.lowercased())
        )
    }

    private func topic(id: String, name: String) throws -> TopicSearchResult {
        try JSONDecoder().decode(
            TopicSearchResult.self,
            from: Data(
                #"{"__entityType":"topic","id":"\#(id)","name":"\#(name)","slug":"example","topicType":"rewards_program"}"#
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
private final class RenderedPointValuationServiceStub: PointValuationServicing {
    let createResult: Result<PointValuation, Error>
    let updateResult: Result<PointValuation, Error>
    let deleteResult: Result<Void, Error>
    var suspendDelete: Bool
    var createCalls = 0
    var updateCalls = 0
    var deleteIds: [String] = []
    private var deleteContinuation: CheckedContinuation<Void, Never>?

    init(
        createResult: Result<PointValuation, Error> = .failure(RenderedPointValuationError.expected),
        updateResult: Result<PointValuation, Error> = .failure(RenderedPointValuationError.expected),
        deleteResult: Result<Void, Error> = .success(()),
        suspendDelete: Bool = false
    ) {
        self.createResult = createResult
        self.updateResult = updateResult
        self.deleteResult = deleteResult
        self.suspendDelete = suspendDelete
    }

    func valuations(after _: String?) async throws -> PointValuationPage {
        .init(results: [])
    }

    func searchRewardsPrograms(query _: String) async throws -> [TopicSearchResult] {
        []
    }

    func create(body _: CreatePointValuationBody) async throws -> PointValuation {
        createCalls += 1
        return try createResult.get()
    }

    func update(id _: String, body _: UpdatePointValuationBody) async throws -> PointValuation {
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

    func resumeDelete() {
        suspendDelete = false
        deleteContinuation?.resume()
        deleteContinuation = nil
    }
}

private enum RenderedPointValuationError: Error {
    case expected
}
