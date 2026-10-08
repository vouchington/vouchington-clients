import Foundation
import Observation
import ViewInspector
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class CommunityAutomodRenderedControlsTests: NativeRouteSurfaceViewModelTestCase {
    func testRenderedFlagNavigatesNativelyAndDisablesDismissUntilResponse() async throws {
        let queuePath = "/api/v1/communities/test-community/moderation-queue"
        CannedFeedURLProtocol.handlers[queuePath] = (
            ApiFixtureLoader.data("native.communities.moderation-queue.automod-flag.page-1"), 200
        )
        let model = try makeModel()
        await model.loadNextPage()
        let entry = try XCTUnwrap(model.pagination.items.first)
        let postId = try XCTUnwrap(entry.automodFlagPostId)
        let path = "/api/v1/communities/test-community/posts/\(postId)/automod-flag/dismissal"
        CannedFeedURLProtocol.handlers[path] = (Data(), 204)
        CannedFeedURLProtocol.handlers[queuePath] = (
            ApiFixtureLoader.data("native.communities.moderation-queue.automod-flag.page-2"), 200
        )
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        var navigatedPaths: [String] = []
        let view = CommunityAutomodWorkspaceView(viewModel: model, onNavigate: { navigatedPaths.append($0) })
        try await ViewHosting.host(view) {
            try view.inspect().find(button: "Flagged post").tap()
            XCTAssertEqual(navigatedPaths, ["/discussion/flagged-post"])
            let barrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "POST")
            try view.inspect().find(button: "Dismiss").tap()
            _ = try await barrier.wait()
            XCTAssertTrue(try view.inspect().find(button: "Dismiss").isDisabled())
            XCTAssertNoThrow(try view.inspect().find(text: "Flagged post"))
            CannedFeedURLProtocol.releaseResponse(path: path)
            await waitForCommit {
                model.notice != nil && !model.dismissingPostIds.contains(postId) && !model.pagination.isLoading
            }
            XCTAssertThrowsError(try view.inspect().find(text: "Flagged post"))
            XCTAssertNoThrow(try view.inspect().find(text: "Next flagged post"))
            XCTAssertNoThrow(try view.inspect().find(text: "Automod flag dismissed"))
        }
    }

    func testRenderedPickerCommitsTheServerActionAndShowsPendingState() async throws {
        let path = "/api/v1/communities/test-community/automod-settings"
        CannedFeedURLProtocol.handlers[path] = (
            ApiFixtureLoader.data("web.communities.automod-settings.update.default"), 200
        )
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let model = try makeModel()
        let form = CommunityAutomodActionForm(viewModel: model)
        try await ViewHosting.host(form) {
            try form.inspect().find(ViewType.Picker.self)
                .select(value: Optional(CommunityAutomodActionSetting.unpublish))
            XCTAssertEqual(model.selectedAction, .unpublish)
            XCTAssertEqual(model.committedAction, .reviewQueue)
            let barrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "PATCH")
            try form.inspect().find(button: "Save automod action").tap()
            let request = try await barrier.wait()
            XCTAssertTrue(try form.inspect().find(ViewType.Picker.self).isDisabled())
            XCTAssertTrue(try form.inspect().find(button: "Saving...").isDisabled())
            XCTAssertEqual(model.committedAction, .reviewQueue)
            let body = try XCTUnwrap(request.body?.data(using: .utf8))
            let json = try XCTUnwrap(JSONSerialization.jsonObject(with: body) as? [String: String])
            XCTAssertEqual(json, ["automod_action": "unpublish"])
            CannedFeedURLProtocol.releaseResponse(path: path)
            await waitForCommit { !model.isSavingAction && model.notice != nil }
            XCTAssertEqual(model.committedAction, .reviewQueue)
            XCTAssertEqual(model.selectedAction, .reviewQueue)
            XCTAssertFalse(try form.inspect().find(ViewType.Picker.self).isDisabled())
        }
    }

    func testSettingsFailurePreservesCommittedActionAndRendersError() async throws {
        let path = "/api/v1/communities/test-community/automod-settings"
        CannedFeedURLProtocol.handlers[path] = (Data(#"{"error":"unavailable"}"#.utf8), 500)
        let model = try makeModel()
        model.selectedAction = .unpublish
        let settings = CommunityAutomodSettingsView(viewModel: model)
        try await ViewHosting.host(settings) {
            try settings.inspect().find(button: "Save automod action").tap()
            await waitForCommit { !model.isSavingAction && model.mutationError != nil }
            XCTAssertEqual(model.committedAction, .reviewQueue)
            XCTAssertEqual(model.selectedAction, .unpublish)
            XCTAssertNil(model.notice)
            XCTAssertNoThrow(try settings.inspect().find(text: "Could not save the automod action."))
            XCTAssertFalse(try settings.inspect().find(ViewType.Picker.self).isDisabled())
        }
    }

    private func makeModel() throws -> CommunityAutomodWorkspaceViewModel {
        try CommunityAutomodWorkspaceViewModel(
            client: makeClient(), slug: "test-community", action: .reviewQueue, canModerate: true
        )
    }

    private func waitForCommit(_ condition: @escaping @MainActor () -> Bool) async {
        let committed = expectation(description: "automod mutation reaches its committed state")
        var active = true
        defer { active = false }
        func observe() {
            guard active else { return }
            if condition() {
                active = false
                committed.fulfill()
                return
            }
            withObservationTracking {
                _ = condition()
            } onChange: {
                Task { @MainActor in observe() }
            }
        }
        observe()
        await fulfillment(of: [committed], timeout: 2)
    }
}
