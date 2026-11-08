import Foundation
import Observation
import SwiftUI
import ViewInspector
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class CommunityAgentPromptSelectionTests: NativeRouteSurfaceViewModelTestCase {
    private let path = "/api/v1/communities/builders/agent-prompts"

    func testMountedContinuationSelectionKeepsDraftAndTargetsSelectedPrompt() async throws {
        CannedFeedURLProtocol.queuedHandlers[path] = try [
            (page(id: "first", more: true), 200, 0),
            (page(id: "second", more: false), 200, 0)
        ]
        let mutationPath = path + "/second"
        CannedFeedURLProtocol.handlers[mutationPath] = (Data("{}".utf8), 503)
        CannedFeedURLProtocol.suspendResponse(path: mutationPath)
        defer { CannedFeedURLProtocol.releaseResponse(path: mutationPath) }
        let parent = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .moderation,
            currentUserId: "user-1",
            isAdministrator: true
        )
        let model = parent.agentPromptWorkspace
        model.draftPrompt = "Unsent editing draft"
        let controls = CommunityAgentAndAutomodControls(viewModel: parent, isSignedIn: true, showSignIn: {})
        try await ViewHosting.host(controls) {
            await settle { model.pagination.hasLoadedPage }
            XCTAssertEqual(
                try CommunityAgentPromptList(viewModel: model).inspect().vStack().accessibilityLabel().string(),
                UiMessages.string(.nativeSwiftCommunitiesPromptId, locale: .english)
            )
            XCTAssertTrue(try controls.inspect().find(button: "Update").isDisabled())
            try row(in: controls, id: "first").tap()
            XCTAssertEqual(model.selectedPromptId, "first")
            try controls.inspect().find(button: "Load more").tap()
            await settle { model.pagination.items.count == 2 && !model.isLoading }
            XCTAssertEqual(model.selectedPromptId, "first")
            try row(in: controls, id: "second").tap()
            XCTAssertEqual(model.selectedPromptId, "second")
            XCTAssertNoThrow(try row(in: controls, id: "first").find(text: "first"))
            XCTAssertNoThrow(try row(in: controls, id: "second").find(text: "second"))

            XCTAssertTrue(try row(in: controls, id: "second").accessibilityTraits().contains(.isSelected))
            XCTAssertFalse(try row(in: controls, id: "first").accessibilityTraits().contains(.isSelected))
            XCTAssertNoThrow(try controls.inspect().find(text: "Order 1 · gpt-5.4-nano · openai"))
            XCTAssertNoThrow(try controls.inspect().find(text: "Order 2 · gpt-5.4-nano · openai"))
            XCTAssertEqual(model.draftPrompt, "Unsent editing draft")
            XCTAssertFalse(try controls.inspect().find(button: "Update").isDisabled())
            XCTAssertTrue(try controls.inspect().findAll(ViewType.TextField.self).allSatisfy {
                (try? $0.labelView().text().string()) != "Prompt ID"
            })
            let barrier = CannedFeedURLProtocol.requestBarrier(path: mutationPath, method: "PATCH")
            try controls.inspect().find(button: "Update").tap()
            let request = try await barrier.wait()
            XCTAssertEqual(request.url.path, mutationPath)
            let body = try XCTUnwrap(request.body?.data(using: .utf8))
            XCTAssertEqual(
                try JSONSerialization.jsonObject(with: body) as? [String: String],
                ["prompt": "Unsent editing draft"]
            )
            CannedFeedURLProtocol.releaseResponse(path: mutationPath)
            await settle { parent.state != .loading }
            let first = try XCTUnwrap(JSONSerialization.jsonObject(with: page(
                id: "first",
                more: false
            )) as? [String: Any])
            var reordered = try XCTUnwrap(JSONSerialization.jsonObject(with: page(
                id: "second",
                more: false
            )) as? [String: Any])
            reordered["community_agent_prompts"] =
                try XCTUnwrap(reordered["community_agent_prompts"] as? [[String: Any]])
                    + XCTUnwrap(first["community_agent_prompts"] as? [[String: Any]])
            CannedFeedURLProtocol.handlers[path] = try (JSONSerialization.data(withJSONObject: reordered), 200)
            await model.reload()
            XCTAssertEqual(model.pagination.items.map(\.id), ["second", "first"])
            XCTAssertNoThrow(try row(in: controls, id: "first").find(text: "first"))
            XCTAssertNoThrow(try row(in: controls, id: "second").find(text: "second"))
            XCTAssertEqual(model.selectedPromptId, "second")

        }
    }

    func testSelectionIsLoadedOnlyAndReloadClearsTargetWithoutChangingDraft() async throws {
        CannedFeedURLProtocol.handlers[path] = try (page(id: "first", more: false), 200)
        let model = try CommunityAgentPromptWorkspace(client: makeClient(), slug: "builders")
        await model.reload()
        model.draftPrompt = "Independent draft"
        model.selectPrompt("not-loaded")
        XCTAssertNil(model.selectedPromptId)
        model.selectPrompt("first")
        XCTAssertEqual(model.selectedPrompt?.id, "first")
        CannedFeedURLProtocol.handlers[path] = try (page(id: "replacement", more: false), 200)
        await model.reload()
        XCTAssertNil(model.selectedPromptId)
        XCTAssertEqual(model.draftPrompt, "Independent draft")
    }

    func testCreatorPermissionsGateRenderedActionsAndPublicMutationAdmission() async throws {
        for (creator, owns) in [("user-1" as String?, true), ("another-user", false), (nil, false)] {
            var object = try XCTUnwrap(JSONSerialization.jsonObject(
                with: ApiFixtureLoader.data("web.communities.agent-prompts.default")
            ) as? [String: Any])
            var prompts = try XCTUnwrap(object["community_agent_prompts"] as? [[String: Any]])
            prompts[0]["created_by_id"] = creator as Any? ?? NSNull()
            object["community_agent_prompts"] = prompts
            CannedFeedURLProtocol.handlers[path] = try (JSONSerialization.data(withJSONObject: object), 200)
            let parent = try CommunityDetailViewModel(
                client: makeClient(), slug: "builders", initialTab: .moderation,
                currentUserId: "user-1", isAdministrator: true
            )
            await parent.agentPromptWorkspace.reload()
            parent.agentPromptWorkspace.selectPrompt("prompt-1")
            let controls = CommunityAgentAndAutomodControls(viewModel: parent, isSignedIn: true, showSignIn: {})
            for title in ["Update", "Allocate", "Deallocate"] {
                XCTAssertEqual(try controls.inspect().find(button: title).isDisabled(), !owns, title)
            }
            XCTAssertFalse(try controls.inspect().find(button: "Delete").isDisabled())
            if !owns {
                let count = CannedFeedURLProtocol.capturedMethods.count
                await parent.updateCommunityAgentPrompt(promptId: "prompt-1", prompt: "No permission")
                await parent.allocateCommunityAgentPrompt(promptId: "prompt-1")
                await parent.deallocateCommunityAgentPrompt(promptId: "prompt-1")
                XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.count, count)
            }
        }
    }

    private func row(in view: some View, id: String) throws -> InspectableView<ViewType.Button> {
        try XCTUnwrap(view.inspect().findAll(ViewType.Button.self).first {
            (try? $0.accessibilityIdentifier()) == "community-agent-prompt-\(id)"
        })
    }

    private func page(id: String, more: Bool) throws -> Data {
        var object = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("web.communities.agent-prompts.default")
        ) as? [String: Any])
        var prompts = try XCTUnwrap(object["community_agent_prompts"] as? [[String: Any]])
        prompts[0]["id"] = id
        prompts[0]["prompt"] = "Identical prompt text"
        object["community_agent_prompts"] = prompts
        object["page_info"] = [
            "has_next_page": more,
            "start_cursor": NSNull(),
            "end_cursor": more ? "opaque-next" as Any : NSNull()
        ]
        return try JSONSerialization.data(withJSONObject: object)
    }

    private func settle(_ condition: @escaping @MainActor () -> Bool) async {
        let completed = expectation(description: "Selection state committed")
        var active = true
        defer { active = false }
        func observe() {
            guard active else { return }
            if condition() {
                active = false
                completed.fulfill()
                return
            }
            withObservationTracking { _ = condition() } onChange: {
                Task { @MainActor in observe() }
            }
        }
        observe()
        await fulfillment(of: [completed], timeout: 2)
    }
}
