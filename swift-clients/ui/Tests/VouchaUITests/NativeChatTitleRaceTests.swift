import Foundation
@testable import VouchaFeatures
@testable import VouchaModels
import XCTest

@MainActor
final class NativeChatTitleRaceTests: NativeRouteSurfaceViewModelTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.contentTypes = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
    }

    func testViewModelSkipsDelayedLocalTitleWhenUserRenamesDuringGeneration() async {
        let provider = DelayedTitleProvider(title: "Generated title")
        let resolver = RaceTitleProviderResolver(
            defaultSelectionValue: .appleFoundationModels,
            providers: [.appleFoundationModels: provider]
        )
        let viewModel = NativeChatViewModel(client: nil, routeMatch: nil, titleProviderResolver: resolver)
        viewModel.conversations = [makeConversation(id: "conversation-1", title: "", updatedAt: "2026-01-01T00:00:00Z")]
        viewModel.selectedConversationId = "conversation-1"
        viewModel.conversationTitleDraft = ""
        viewModel.messages = [
            .init(id: "message-1", role: .user, content: "Need a title", isStreaming: false)
        ]

        let generation = Task { @MainActor in
            await viewModel.generateTitleIfNeeded(conversationId: "conversation-1")
        }
        await provider.waitUntilTitleRequested()
        viewModel.conversationTitleDraft = "Manual title"
        viewModel.updateConversation(id: "conversation-1") { $0.title = "Manual title" }
        await provider.resumeTitleGeneration()
        await generation.value

        XCTAssertEqual(viewModel.conversationTitleDraft, "Manual title")
        XCTAssertEqual(viewModel.conversations.first?.title, "Manual title")
    }

    func testViewModelSkipsDelayedLocalTitlePersistenceWhenUserRenames() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/conversations/conversation-1"] = [
            (Self.generatedConversationData, 200, 0.1),
            (Self.manualConversationData, 200, 0)
        ]

        let provider = ImmediateTitleProvider(title: "Local title")
        let resolver = RaceTitleProviderResolver(
            defaultSelectionValue: .appleFoundationModels,
            providers: [.appleFoundationModels: provider]
        )
        let client = try makeClient()
        let viewModel = NativeChatViewModel(client: client, routeMatch: nil, titleProviderResolver: resolver)
        viewModel.conversations = [makeConversation(id: "conversation-1", title: "", updatedAt: "2026-01-01T00:00:00Z")]
        viewModel.selectedConversationId = "conversation-1"
        viewModel.conversationTitleDraft = ""
        viewModel.messages = [
            .init(id: "message-1", role: .user, content: "Need a title", isStreaming: false)
        ]

        let generation = Task { @MainActor in
            await viewModel.generateTitleIfNeeded(conversationId: "conversation-1")
        }
        for _ in 0 ..< 200
            where CannedFeedURLProtocol.capturedURLs.contains(where: {
                $0.path == "/api/v1/my/conversations/conversation-1"
            }) == false {
            try await Task.sleep(nanoseconds: 1_000_000)
        }
        viewModel.conversationTitleDraft = "Manual title"
        viewModel.updateConversation(id: "conversation-1") { $0.title = "Manual title" }
        await generation.value

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["PATCH", "PATCH"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedBodies.first??.contains(#""title":"Local title""#), true)
        XCTAssertEqual(CannedFeedURLProtocol.capturedBodies.last??.contains(#""title":"Manual title""#), true)
        XCTAssertEqual(viewModel.conversationTitleDraft, "Manual title")
        XCTAssertEqual(viewModel.conversations.first?.title, "Manual title")
    }
}

private actor DelayedTitleProvider: NativeChatTitleProviding {
    nonisolated let kind = NativeChatTitleProviderKind.appleFoundationModels
    nonisolated let status = NativeChatTitleProviderStatus(isAvailable: true, detail: nil)
    private let title: String
    private var started = false
    private var startContinuation: CheckedContinuation<Void, Never>?
    private var titleContinuation: CheckedContinuation<Void, Never>?

    init(title: String) {
        self.title = title
    }

    func waitUntilTitleRequested() async {
        if started {
            return
        }
        await withCheckedContinuation { continuation in
            startContinuation = continuation
        }
    }

    func resumeTitleGeneration() {
        titleContinuation?.resume()
        titleContinuation = nil
    }

    func generateAssistantResponse(
        to _: String,
        history _: [NativeChatTimelineMessage]
    ) async throws -> NativeChatAssistantResponse? {
        nil
    }

    func generateTitle(from _: [NativeChatTimelineMessage]) async throws -> String? {
        await withCheckedContinuation { continuation in
            titleContinuation = continuation
            started = true
            startContinuation?.resume()
            startContinuation = nil
        }
        return title
    }
}

private final class ImmediateTitleProvider: NativeChatTitleProviding, @unchecked Sendable {
    let kind = NativeChatTitleProviderKind.appleFoundationModels
    let status = NativeChatTitleProviderStatus(isAvailable: true, detail: nil)
    private let title: String

    init(title: String) {
        self.title = title
    }

    func generateAssistantResponse(
        to _: String,
        history _: [NativeChatTimelineMessage]
    ) async throws -> NativeChatAssistantResponse? {
        nil
    }

    func generateTitle(from _: [NativeChatTimelineMessage]) async throws -> String? {
        title
    }
}

private struct RaceTitleProviderResolver: NativeChatTitleProviderResolving, @unchecked Sendable {
    let defaultSelectionValue: NativeChatTitleProviderKind
    let providers: [NativeChatTitleProviderKind: any NativeChatTitleProviding]

    func defaultSelection() -> NativeChatTitleProviderKind {
        defaultSelectionValue
    }

    func provider(for kind: NativeChatTitleProviderKind) -> any NativeChatTitleProviding {
        providers[kind] ?? providers[defaultSelectionValue] ?? NativeChatHostedTitleProvider(kind: kind)
    }
}

private extension NativeChatTitleRaceTests {
    func makeConversation(
        id: String,
        title: String,
        updatedAt: String
    ) -> ChatConversation {
        ChatConversation(
            id: id,
            title: title,
            createdAt: Date(timeIntervalSince1970: 0),
            createdById: "user-1",
            updatedAt: ISO8601DateFormatter().date(from: updatedAt) ?? Date(timeIntervalSince1970: 0),
            updatedById: nil,
            deletedAt: nil,
            deletedById: nil,
            lastResponseId: nil
        )
    }

    static let generatedConversationData = Data(
        """
        {
          "conversation": {
            "id": "conversation-1",
            "title": "Generated chat",
            "created_at": "2026-01-01T00:00:00Z",
            "created_by_id": "user-1",
            "updated_at": "2026-01-01T00:02:00Z",
            "updated_by_id": "user-1",
            "deleted_at": null,
            "deleted_by_id": null
          }
        }
        """.utf8
    )

    static let manualConversationData = Data(
        """
        {
          "conversation": {
            "id": "conversation-1",
            "title": "Manual title",
            "created_at": "2026-01-01T00:00:00Z",
            "created_by_id": "user-1",
            "updated_at": "2026-01-01T00:03:00Z",
            "updated_by_id": "user-1",
            "deleted_at": null,
            "deleted_by_id": null
          }
        }
        """.utf8
    )
}
