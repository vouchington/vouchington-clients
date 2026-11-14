import Foundation
import SwiftUI
import ViewInspector
import VouchaAPI
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class NativeTopicManagementMergeStatusTests: NativeRouteSurfaceViewModelTestCase {
    private static let sourceId = "70000000-0000-7000-8000-000000005170"
    private static let operationId = "70000000-0000-7000-8000-000000005172"
    private static let mergesPath = "/api/v1/topics/\(sourceId)/merges"
    private static let statusPath = "\(mergesPath)/\(operationId)"

    func testAcceptedMergeShowsQueuedAndKeepsOperation() async throws {
        let viewModel = try await acceptedViewModel()

        XCTAssertEqual(viewModel.mergeMessage, .settingsTopicsMergeQueued)
        XCTAssertEqual(viewModel.mergeOperation?.id, Self.operationId)
        XCTAssertTrue(viewModel.canCheckMergeStatus)
        guard case .loaded = viewModel.state else { return XCTFail("Expected loaded state") }
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["POST"])
    }

    func testCheckStatusMapsEachFixtureState() async throws {
        let expectations: [(String, UiMessageKey, Bool)] = [
            ("queued", .settingsTopicsMergeQueued, true),
            ("running", .settingsTopicsMergeQueued, true),
            ("completed", .settingsTopicsMergeCompleted, false),
            ("failed", .settingsTopicsMergeFailed, false)
        ]
        for (state, key, canCheckAgain) in expectations {
            CannedFeedURLProtocol.reset()
            let viewModel = try await acceptedViewModel()
            CannedFeedURLProtocol.handlers[Self.statusPath] = (
                ApiFixtureLoader.data("native.topics.merge.status.\(state)"),
                200
            )

            await viewModel.checkMergeStatus()

            XCTAssertEqual(viewModel.mergeMessage, key, state)
            XCTAssertEqual(viewModel.canCheckMergeStatus, canCheckAgain, state)
            XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["POST", "GET"], state)
            XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.path, Self.statusPath, state)
            XCTAssertFalse(viewModel.isCheckingMerge)
        }
    }

    func testStatusReadFailureShowsStatusFailedAndKeepsOperation() async throws {
        let viewModel = try await acceptedViewModel()
        CannedFeedURLProtocol.handlers[Self.statusPath] = (Data(#"{"error":"boom"}"#.utf8), 500)

        await viewModel.checkMergeStatus()

        XCTAssertEqual(viewModel.mergeMessage, .settingsTopicsMergeStatusFailed)
        XCTAssertEqual(viewModel.mergeOperation?.id, Self.operationId)
        XCTAssertTrue(viewModel.canCheckMergeStatus)
        XCTAssertFalse(viewModel.isCheckingMerge)
    }

    func testCheckStatusWithoutOperationDoesNotRequest() async throws {
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/topic-1/settings/merge")?.match)
        let viewModel = try NativeTopicManagementViewModel(client: makeClient(), routeMatch: match)

        await viewModel.checkMergeStatus()

        XCTAssertNil(viewModel.mergeMessage)
        XCTAssertFalse(viewModel.canCheckMergeStatus)
        XCTAssertTrue(CannedFeedURLProtocol.capturedMethods.isEmpty)
    }

    func testSecondMergeIsIgnoredWhileOperationExists() async throws {
        let viewModel = try await acceptedViewModel()

        await viewModel.mergeTopic()

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["POST"])
    }

    func testMergeFieldsShowStatusAndCheckAction() async throws {
        let viewModel = try await acceptedViewModel()
        let sut = NativeTopicManagementMergeFields(viewModel: viewModel)
        let locale = Locale(identifier: "en")

        XCTAssertNoThrow(try sut.inspect().find(text: UiMessages.string(.settingsTopicsMergeQueued, locale: locale)))
        let button = try sut.inspect().find(button: UiMessages.string(.settingsTopicsMergeCheckStatus, locale: locale))
        CannedFeedURLProtocol.handlers[Self.statusPath] = (
            ApiFixtureLoader.data("native.topics.merge.status.completed"),
            200
        )
        try button.tap()
        let deadline = ContinuousClock.now.advanced(by: .seconds(2))
        while viewModel.mergeMessage != .settingsTopicsMergeCompleted, ContinuousClock.now < deadline {
            try await Task.sleep(for: .milliseconds(10))
        }
        XCTAssertEqual(viewModel.mergeMessage, .settingsTopicsMergeCompleted)
    }

    func testFailedReadRetriesOnlyAcceptedOperationGET() async throws {
        let viewModel = try await acceptedViewModel()
        let accepted = viewModel.mergeOperation
        CannedFeedURLProtocol.handlers[Self.statusPath] = (Data(), 500)
        await viewModel.checkMergeStatus()
        CannedFeedURLProtocol.handlers[Self.statusPath] = (
            ApiFixtureLoader.data("native.topics.merge.status.completed"), 200
        )
        await viewModel.checkMergeStatus()
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["POST", "GET", "GET"])
        XCTAssertEqual(viewModel.mergeOperation?.id, accepted?.id)
        XCTAssertEqual(viewModel.mergeOperation?.statusUrl, accepted?.statusUrl)
        XCTAssertEqual(viewModel.mergeOperation?.status, "completed")
    }

    func testDuplicateSubmissionWhilePOSTPendingIsIgnoredAndAccountChangeDiscardsResponse() async throws {
        CannedFeedURLProtocol.handlers[Self.mergesPath] = (
            ApiFixtureLoader.data("native.topics.merge.accepted"), 202
        )
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(
            for: "/topic/\(Self.sourceId)/settings/merge"
        )?.match)
        let viewModel = try NativeTopicManagementViewModel(client: makeClient(), routeMatch: match, accountId: "first")
        viewModel.destinationIdOrSlug = "destination"
        CannedFeedURLProtocol.suspendResponse(path: Self.mergesPath)
        let barrier = CannedFeedURLProtocol.requestBarrier(path: Self.mergesPath, method: "POST")
        let submission = Task { await viewModel.mergeTopic() }
        _ = try await barrier.wait()
        await viewModel.mergeTopic()
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["POST"])
        viewModel.updateMergeAccount("second")
        CannedFeedURLProtocol.releaseResponse(path: Self.mergesPath)
        await submission.value
        XCTAssertNil(viewModel.mergeOperation)
        XCTAssertNil(viewModel.mergeMessage)
        XCTAssertFalse(viewModel.isSubmittingMerge)
    }

    func testAccountChangeDiscardsPendingGETAndClearsAcceptedIdentity() async throws {
        let viewModel = try await acceptedViewModel()
        CannedFeedURLProtocol.handlers[Self.statusPath] = (
            ApiFixtureLoader.data("native.topics.merge.status.completed"), 200
        )
        CannedFeedURLProtocol.suspendResponse(path: Self.statusPath)
        let barrier = CannedFeedURLProtocol.requestBarrier(path: Self.statusPath, method: "GET")
        let read = Task { await viewModel.checkMergeStatus() }
        _ = try await barrier.wait()
        viewModel.updateMergeAccount("next-account")
        CannedFeedURLProtocol.releaseResponse(path: Self.statusPath)
        await read.value
        XCTAssertNil(viewModel.mergeOperation)
        XCTAssertNil(viewModel.mergeMessage)
        XCTAssertFalse(viewModel.isCheckingMerge)
    }

    func testNewerOperationDiscardsPendingGET() async throws {
        let viewModel = try await acceptedViewModel()
        let original = viewModel.mergeOperation
        CannedFeedURLProtocol.handlers[Self.statusPath] = (
            ApiFixtureLoader.data("native.topics.merge.status.completed"), 200
        )
        CannedFeedURLProtocol.suspendResponse(path: Self.statusPath)
        let barrier = CannedFeedURLProtocol.requestBarrier(path: Self.statusPath, method: "GET")
        let read = Task { await viewModel.checkMergeStatus() }
        _ = try await barrier.wait()
        let data = ApiFixtureLoader.data("native.topics.merge.accepted")
        let json = try XCTUnwrap(String(data: data, encoding: .utf8))
        CannedFeedURLProtocol.handlers[Self.mergesPath] = (Data(
            json.replacingOccurrences(of: Self.operationId, with: "newer-operation").utf8
        ), 202)
        let response: TopicMergeEnvelope = try await makeClient().send(.mergeTopicAliases(
            sourceTopicId: Self.sourceId, destinationIdOrSlug: "destination"
        ))
        let newer = response.topicMerge
        viewModel.mergeOperation = newer
        CannedFeedURLProtocol.releaseResponse(path: Self.statusPath)
        await read.value
        XCTAssertEqual(viewModel.mergeOperation, newer)
        XCTAssertNotEqual(viewModel.mergeOperation?.id, original?.id)
    }

    func testRenderedProducerStageAndTerminalFields() async throws {
        for status in ["queued", "running", "completed", "failed"] {
            let viewModel = try await acceptedViewModel()
            CannedFeedURLProtocol.handlers[Self.statusPath] = (
                ApiFixtureLoader.data("native.topics.merge.status.\(status)"), 200
            )
            await viewModel.checkMergeStatus()
            let operation = try XCTUnwrap(viewModel.mergeOperation)
            let sut = NativeTopicManagementMergeFields(viewModel: viewModel)
            XCTAssertNoThrow(try sut.inspect().find(text: operation.stage))
            XCTAssertNoThrow(try sut.inspect().find(text: operation.status))
            if let errorCode = operation.errorCode {
                XCTAssertNoThrow(try sut.inspect().find(text: errorCode))
            }
            if operation.completedAt != nil {
                XCTAssertNoThrow(try sut.inspect().find(viewWithAccessibilityIdentifier: "topic-merge-completed-at"))
            }
        }
    }

    private func acceptedViewModel() async throws -> NativeTopicManagementViewModel {
        CannedFeedURLProtocol.handlers[Self.mergesPath] = (
            ApiFixtureLoader.data("native.topics.merge.accepted"),
            202
        )
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(
            for: "/topic/\(Self.sourceId)/settings/merge"
        )?.match)
        let viewModel = try NativeTopicManagementViewModel(client: makeClient(), routeMatch: match)
        viewModel.destinationIdOrSlug = "70000000-0000-7000-8000-000000005171"
        await viewModel.save()
        return viewModel
    }
}
