import Foundation
import Observation
import SwiftUI
import ViewInspector
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class CommunityAgentPromptWorkspaceTests: NativeRouteSurfaceViewModelTestCase {
    private let path = "/api/v1/communities/builders/agent-prompts"

    func testRenderedLoadMoreAppendsDeduplicatesAndRetainsGlobalSlots() async throws {
        CannedFeedURLProtocol.queuedHandlers[path] = try [
            (page(ids: ["prompt-1"], more: true, cursor: "opaque+/="), 200, 0),
            (page(ids: ["prompt-1", "prompt-2"], more: false, cursor: nil), 200, 0)
        ]
        let model = try CommunityAgentPromptWorkspace(client: makeClient(), slug: "builders")
        await model.reload()
        XCTAssertEqual(model.slotInfo?.used, 8)
        XCTAssertEqual(model.pagination.items.count, 1)
        let list = CommunityAgentPromptList(viewModel: model)
        XCTAssertNoThrow(try list.inspect().find(text: "Prompt prompt-1"))
        try list.inspect().find(button: "Load more").tap()
        await settle { !model.pagination.isLoading && model.pagination.items.count == 2 }
        XCTAssertEqual(model.pagination.items.map(\.id), ["prompt-1", "prompt-2"])
        XCTAssertEqual(model.slotInfo?.used, 8)
        XCTAssertEqual(model.slotInfo?.remaining, 2)
        XCTAssertNoThrow(try list.inspect().find(text: "Prompt prompt-2"))
        XCTAssertThrowsError(try list.inspect().find(button: "Load more"))
        let request = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.last)
        XCTAssertTrue(URLComponents(url: request, resolvingAgainstBaseURL: false)?.queryItems?.contains {
            $0.name == "after" && $0.value == "opaque+/="
        } == true)
    }

    func testEmptyContinuationUsesHasNextPageRatherThanPageCount() async throws {
        CannedFeedURLProtocol.queuedHandlers[path] = try [
            (page(ids: [], more: true, cursor: "next"), 200, 0),
            (page(ids: ["prompt-2"], more: false, cursor: nil), 200, 0)
        ]
        let model = try CommunityAgentPromptWorkspace(client: makeClient(), slug: "builders")
        await model.reload()
        XCTAssertTrue(model.pagination.hasMore)
        await model.loadMore()
        XCTAssertEqual(model.pagination.items.map(\.id), ["prompt-2"])
    }

    func testCreationLimitPreservesRowsDraftAndAllowsRetryDespiteGlobalSlots() async throws {
        CannedFeedURLProtocol.handlers[path] = try (page(ids: ["prompt-1"], more: false, cursor: nil), 200)
        let parent = try CommunityDetailViewModel(client: makeClient(), slug: "builders", initialTab: .moderation)
        let model = parent.agentPromptWorkspace
        var error = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("native.chat.identity-conflict")
        ) as? [String: Any])
        error["code"] = "COMMUNITY_AGENT_PROMPT_LIMIT_REACHED"
        error["message"] = "Community agent prompt limit reached"
        let conflict = try JSONSerialization.data(withJSONObject: error)
        CannedFeedURLProtocol.requestHandlers["POST \(path)"] = (conflict, 409)
        let controls = CommunityAgentAndAutomodControls(viewModel: parent, isSignedIn: true, showSignIn: {})
        try await ViewHosting.host(controls) {
            await settle { model.pagination.hasLoadedPage }
            let field = try controls.inspect().findAll(ViewType.TextField.self).first {
                (try? $0.labelView().text().string()) == "Prompt text"
            }
            try XCTUnwrap(field).setInput("Keep this draft")
            XCTAssertEqual(model.draftPrompt, "Keep this draft")
            await Task.yield()
            try controls.inspect().find(button: "Create").tap()
            await settle { model.creationLimitReached && !model.isCreating }
            let retainedField = try controls.inspect().findAll(ViewType.TextField.self).first {
                (try? $0.labelView().text().string()) == "Prompt text"
            }
            XCTAssertEqual(try XCTUnwrap(retainedField).input(), "Keep this draft")
            XCTAssertTrue(model.creationLimitReached)
            XCTAssertEqual(model.pagination.items.map(\.id), ["prompt-1"])
            XCTAssertNoThrow(try controls.inspect().find(text: "Community agent prompt limit reached"))
            XCTAssertFalse(model.isCreating)
            XCTAssertFalse(try controls.inspect().find(button: "Create").isDisabled())
            await parent.createCommunityAgentPrompt(prompt: "Keep this draft")
        }
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.filter { $0 == "POST" }.count, 2)
    }

    func testDisappearanceFencesDelayedPageAndAccountRouteIdentityChanges() async throws {
        CannedFeedURLProtocol.handlers[path] = try (page(ids: ["late"], more: false, cursor: nil), 200)
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let model = try CommunityAgentPromptWorkspace(client: makeClient(), slug: "builders")
        let load = Task { await model.reload() }
        _ = try await CannedFeedURLProtocol.requestBarrier(path: path, method: "GET").wait()
        try CommunityAgentPromptList(viewModel: model).inspect().vStack().callOnDisappear()
        CannedFeedURLProtocol.releaseResponse(path: path)
        await load.value
        XCTAssertTrue(model.pagination.items.isEmpty)
        XCTAssertNil(model.slotInfo)
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/communities/builders/agent-prompts"))
        let first = NativeRouteDestinationView(entry: route.entry, routeMatch: route.match, currentUserId: "first")
        let second = NativeRouteDestinationView(entry: route.entry, routeMatch: route.match, currentUserId: "second")
        XCTAssertNotEqual(first.routeIdentity, second.routeIdentity)
    }

    func testReloadSupersedesDelayedPageAndDuplicateLoadMoreDoesNotSend() async throws {
        CannedFeedURLProtocol.handlers[path] = try (page(ids: ["stale"], more: true, cursor: "old"), 200)
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let model = try CommunityAgentPromptWorkspace(client: makeClient(), slug: "builders")
        let firstBarrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let first = Task { await model.reload() }
        _ = try await firstBarrier.wait()
        await model.loadMore()
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.count, 1)
        CannedFeedURLProtocol.handlers[path] = try (page(ids: ["fresh"], more: false, cursor: nil), 200)
        let secondBarrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let second = Task { await model.reload() }
        _ = try await secondBarrier.wait()
        CannedFeedURLProtocol.releaseResponse(path: path)
        await first.value
        await second.value
        XCTAssertEqual(model.pagination.items.map(\.id), ["fresh"])
        XCTAssertFalse(model.pagination.hasMore)
    }

    func testDisappearanceFencesDelayedCreationRefusal() async throws {
        let model = try CommunityAgentPromptWorkspace(client: makeClient(), slug: "builders")
        CannedFeedURLProtocol.handlers[path] = (
            Data(#"{"code":"COMMUNITY_AGENT_PROMPT_LIMIT_REACHED","message":"Community agent prompt limit reached"}"#
                .utf8), 409
        )
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let barrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "POST")
        let create = Task { await model.create(prompt: "Draft") }
        _ = try await barrier.wait()
        try CommunityAgentPromptList(viewModel: model).inspect().vStack().callOnDisappear()
        CannedFeedURLProtocol.releaseResponse(path: path)
        let created = await create.value
        XCTAssertFalse(created)
        XCTAssertFalse(model.creationLimitReached)
        XCTAssertNil(model.notice)
        XCTAssertFalse(model.isCreating)
    }

    func testDisappearanceDuringPromptUpdateDoesNotStartListRefresh() async throws {
        let mutationPath = path + "/prompt-1"
        CannedFeedURLProtocol.handlers[mutationPath] = (Data("{}".utf8), 200)
        CannedFeedURLProtocol.suspendResponse(path: mutationPath)
        defer { CannedFeedURLProtocol.releaseResponse(path: mutationPath) }
        let parent = try CommunityDetailViewModel(client: makeClient(), slug: "builders", initialTab: .moderation)
        let barrier = CannedFeedURLProtocol.requestBarrier(path: mutationPath, method: "PATCH")
        let update = Task { await parent.updateCommunityAgentPrompt(promptId: "prompt-1", prompt: "Updated") }
        _ = try await barrier.wait()
        try CommunityAgentPromptList(viewModel: parent.agentPromptWorkspace).inspect().vStack().callOnDisappear()
        CannedFeedURLProtocol.releaseResponse(path: mutationPath)
        await update.value
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains { $0.path == path })
    }

    func testPendingCreationSerializesPromptWritesAndRetainsLimitRefusal() async throws {
        CannedFeedURLProtocol.handlers[path] = (
            Data(#"{"code":"COMMUNITY_AGENT_PROMPT_LIMIT_REACHED","message":"Community agent prompt limit reached"}"#
                .utf8), 409
        )
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let parent = try CommunityDetailViewModel(client: makeClient(), slug: "builders", initialTab: .moderation)
        let barrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "POST")
        let create = Task { await parent.createCommunityAgentPrompt(prompt: "Draft") }
        _ = try await barrier.wait()
        let controls = CommunityAgentAndAutomodControls(viewModel: parent, isSignedIn: true, showSignIn: {})
        for title in ["Create", "Update", "Allocate", "Deallocate", "Delete"] {
            XCTAssertTrue(try controls.inspect().find(button: title).isDisabled(), title)
        }
        await parent.deleteCommunityAgentPrompt(promptId: "prompt-1")
        await parent.updateCommunityAgentPrompt(promptId: "prompt-1", prompt: "Changed")
        await parent.allocateCommunityAgentPrompt(promptId: "prompt-1")
        await parent.deallocateCommunityAgentPrompt(promptId: "prompt-1")
        await parent.createCommunityAgentPrompt(prompt: "Duplicate")
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["POST"])
        var signIns = 0
        let signedOut = CommunityAgentAndAutomodControls(
            viewModel: parent,
            isSignedIn: false,
            showSignIn: { signIns += 1 }
        )
        try signedOut.inspect().find(button: "Update").tap()
        XCTAssertEqual(signIns, 1)
        CannedFeedURLProtocol.releaseResponse(path: path)
        await create.value
        XCTAssertTrue(parent.agentPromptWorkspace.creationLimitReached)
        XCTAssertFalse(try controls.inspect().find(button: "Create").isDisabled())
    }

    func testParentLoadingBlocksCreationAndPromptMutation() async throws {
        let parent = try CommunityDetailViewModel(client: makeClient(), slug: "builders", initialTab: .moderation)
        parent.state = .loading
        await parent.createCommunityAgentPrompt(prompt: "Draft")
        await parent.deleteCommunityAgentPrompt(promptId: "prompt-1")
        await parent.updateCommunityAgentPrompt(promptId: "prompt-1", prompt: "Changed")
        await parent.allocateCommunityAgentPrompt(promptId: "prompt-1")
        await parent.deallocateCommunityAgentPrompt(promptId: "prompt-1")
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
        let controls = CommunityAgentAndAutomodControls(viewModel: parent, isSignedIn: true, showSignIn: {})
        XCTAssertTrue(try controls.inspect().find(button: "Create").isDisabled())
        XCTAssertTrue(try controls.inspect().find(button: "Update").isDisabled())
    }

    func testInitialPageBlocksCreationWithoutLosingContinuationOrGlobalSlots() async throws {
        CannedFeedURLProtocol.handlers[path] = try (page(ids: ["prompt-1"], more: true, cursor: "next-page"), 200)
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let parent = try CommunityDetailViewModel(client: makeClient(), slug: "builders", initialTab: .moderation)
        let model = parent.agentPromptWorkspace
        model.draftPrompt = "Retained draft"
        let barrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let load = Task { await model.reload() }
        _ = try await barrier.wait()
        // An unexpected write must fail immediately rather than hang behind the suspended read.
        CannedFeedURLProtocol.errors[path] = URLError(.notConnectedToInternet)
        let controls = CommunityAgentAndAutomodControls(viewModel: parent, isSignedIn: true, showSignIn: {})
        XCTAssertTrue(try controls.inspect().find(button: "Create").isDisabled())
        let created = await model.create(prompt: model.draftPrompt)
        XCTAssertFalse(created)
        await parent.createCommunityAgentPrompt(prompt: model.draftPrompt)
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["GET"])
        XCTAssertTrue(model.pagination.isLoading)
        CannedFeedURLProtocol.errors.removeValue(forKey: path)
        CannedFeedURLProtocol.releaseResponse(path: path)
        await load.value
        XCTAssertEqual(model.pagination.items.map(\.id), ["prompt-1"])
        XCTAssertEqual(model.slotInfo?.used, 8)
        XCTAssertEqual(model.slotInfo?.remaining, 2)
        XCTAssertEqual(model.pagination.endCursor, "next-page")
        XCTAssertTrue(model.pagination.hasMore)
        XCTAssertEqual(model.draftPrompt, "Retained draft")
        XCTAssertFalse(try controls.inspect().find(button: "Create").isDisabled())
        XCTAssertNoThrow(try CommunityAgentPromptList(viewModel: model).inspect().find(button: "Load more"))
    }

    func testInitialReadErrorAndRetryRemainAvailableAfterCreationLimit() async throws {
        CannedFeedURLProtocol.queuedHandlers[path] = try [
            (Data("{}".utf8), 503, 0),
            (page(ids: ["prompt-1"], more: false, cursor: nil), 200, 0)
        ]
        var conflict = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("native.chat.identity-conflict")
        ) as? [String: Any])
        conflict["code"] = "COMMUNITY_AGENT_PROMPT_LIMIT_REACHED"
        conflict["message"] = "Community agent prompt limit reached"
        CannedFeedURLProtocol.requestHandlers["POST \(path)"] = try (
            JSONSerialization.data(withJSONObject: conflict),
            409
        )
        let parent = try CommunityDetailViewModel(client: makeClient(), slug: "builders", initialTab: .moderation)
        let model = parent.agentPromptWorkspace
        await model.reload()
        XCTAssertNotNil(model.pagination.lastError)
        await parent.createCommunityAgentPrompt(prompt: "Retained draft")
        XCTAssertTrue(model.creationLimitReached)
        XCTAssertNotNil(model.pagination.lastError)
        let list = CommunityAgentPromptList(viewModel: model)
        try list.inspect().find(button: UiMessages.string(.nativeCommonRetry, locale: .english)).tap()
        await settle { model.pagination.hasLoadedPage && !model.pagination.isLoading }
        XCTAssertEqual(model.pagination.items.map(\.id), ["prompt-1"])
        XCTAssertEqual(model.slotInfo?.used, 8)
        XCTAssertNil(model.pagination.lastError)
        XCTAssertThrowsError(try list.inspect().find(button: UiMessages.string(.nativeCommonRetry, locale: .english)))
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["GET", "POST", "GET"])
    }

    private func page(ids: [String], more: Bool, cursor: String?) throws -> Data {
        var object = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("web.communities.agent-prompts.default")
        ) as? [String: Any])
        let template = try XCTUnwrap((object["community_agent_prompts"] as? [[String: Any]])?.first)
        object["community_agent_prompts"] = ids.map { id in
            var prompt = template
            prompt["id"] = id
            prompt["prompt"] = "Prompt \(id)"
            return prompt
        }
        object["page_info"] = [
            "has_next_page": more,
            "start_cursor": NSNull(),
            "end_cursor": cursor as Any? ?? NSNull()
        ]
        object["slot_info"] = ["used": 8, "limit": 10, "remaining": 2, "limits_by_plan": ["plus": 3, "pro": 10]]
        return try JSONSerialization.data(withJSONObject: object)
    }

    private func settle(_ condition: @escaping @MainActor () -> Bool) async {
        let completed = expectation(description: "Prompt state committed")
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
