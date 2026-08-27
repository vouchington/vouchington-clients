import Foundation
import ViewInspector
@testable import VouchaFeatures
@testable import VouchaModels
import XCTest

@MainActor
final class NativeChatSupportSurfaceTests: NativeRouteSurfaceViewModelTestCase {
    func testChatViewModelAppliesStreamingEventsIntoTimeline() {
        let viewModel = NativeChatViewModel(client: nil, routeMatch: nil)

        viewModel.apply(
            event: .metadata(
                .init(
                    conversationId: "conversation-1",
                    userMessageId: "user-message-1",
                    assistantMessageId: "assistant-message-1",
                    jobId: "job-1"
                )
            )
        )
        viewModel.apply(event: .text("Hello"))
        viewModel.apply(event: .toolCall(.init(toolCallId: "tool-1", name: "search", arguments: "{}")))
        viewModel.apply(event: .subagentStep(.init(agentName: "helper", toolName: "search", toolCallId: "tool-1")))
        viewModel.apply(
            event: .subagentText(.init(agentName: "helper", toolCallId: "tool-1", content: "Working"))
        )
        viewModel.apply(event: .toolResult(toolCallId: "tool-1", result: .string("Found it")))
        viewModel.apply(event: .error("Stream failed"))

        XCTAssertEqual(viewModel.streamErrorMessage, .verbatim("Stream failed"))
        XCTAssertEqual(uiEnglish(viewModel.messages.first?.error), "Stream failed")

        viewModel.apply(event: .metadata(.init(
            conversationId: "conversation-1",
            userMessageId: "user-message-2",
            assistantMessageId: "assistant-message-2",
            jobId: "job-2"
        )))
        viewModel.apply(event: .text("Recovered"))
        viewModel.apply(event: .done)

        XCTAssertEqual(viewModel.messages.count, 2)
        XCTAssertEqual(viewModel.messages.first?.role, .assistant)
        XCTAssertEqual(viewModel.messages.first?.content, "Hello")
        XCTAssertEqual(viewModel.messages.first?.toolCalls.count, 1)
        XCTAssertEqual(viewModel.messages.first?.toolResults.count, 1)
        XCTAssertEqual(uiEnglish(viewModel.messages.first?.toolResults.first?.displayText), "Found it")
        XCTAssertEqual(viewModel.messages.first?.subagentSteps.count, 1)
        XCTAssertEqual(viewModel.messages.first?.subagentTextChunks.count, 1)
        XCTAssertEqual(viewModel.messages.last?.content, "HelloRecovered")
        XCTAssertFalse(viewModel.isStreaming)
        XCTAssertFalse(viewModel.messages.first?.isStreaming ?? true)
    }

    func testSupportViewModelPrefillsConversationIdFromRouteQueryItem() {
        let viewModel = NativeSupportViewModel(
            client: nil,
            routeMatch: NativeRouteMatch(
                path: "/chat/support/new?conversation_id=conversation-1",
                template: "/chat/support/new",
                queryItems: ["conversation_id": "conversation-1"]
            )
        )

        XCTAssertNil(viewModel.selectedThreadId)
        XCTAssertEqual(viewModel.conversationId, "conversation-1")
    }

    func testSupportViewModelCreatesThreadAndLoadsDetail() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/support-threads"] = [
            (Self.supportThreadsListData, 200, 0),
            (Self.createdThreadData, 201, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/my/support-threads/thread-1"] = (Self.supportThreadDetailData, 200)
        let client = try makeClient()
        let viewModel = NativeSupportViewModel(client: client, routeMatch: nil)
        await viewModel.load()
        viewModel.subject = "Need help"
        viewModel.message = "Initial note"
        viewModel.conversationId = "conversation-1"

        await viewModel.createThread()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/my/support-threads",
            "/api/v1/my/support-threads",
            "/api/v1/my/support-threads/thread-1"
        ])
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["GET", "POST", "GET"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedBodies[1]?.contains(#""subject":"Need help""#), true)
        XCTAssertEqual(CannedFeedURLProtocol.capturedBodies[1]?.contains(#""message":"Initial note""#), true)
        XCTAssertEqual(CannedFeedURLProtocol.capturedBodies[1]?.contains(#""conversation_id":"conversation-1""#), true)
        XCTAssertEqual(viewModel.selectedThreadId, "thread-1")
        XCTAssertEqual(viewModel.threads.count, 2)
        XCTAssertEqual(viewModel.threads.first?.subject, "Need help")
        XCTAssertEqual(viewModel.subject, "")
        XCTAssertEqual(viewModel.message, "")
        XCTAssertEqual(viewModel.conversationId, "")
        XCTAssertEqual(viewModel.selectedThreadMessages.first?.bodyText, "Initial note")
    }

    func testSupportViewModelInsertsDeepLinkedThreadDetailWhenMissingFromList() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/support-threads"] = [
            (Self.supportThreadsListData, 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/my/support-threads/thread-1"] = (Self.supportThreadDetailData, 200)
        let client = try makeClient()
        let viewModel = NativeSupportViewModel(
            client: client,
            routeMatch: NativeRouteMatch(
                path: "/chat/support/thread-1",
                template: "/chat/support/:threadId",
                params: ["threadId": "thread-1"]
            )
        )

        await viewModel.load()

        XCTAssertEqual(viewModel.selectedThread?.subject, "Need help")
        XCTAssertEqual(viewModel.threads.map(\.id), ["thread-1", "thread-0"])
    }

    func testSupportViewModelLoadsMoreStartsNewThreadAndReportsErrors() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/support-threads"] = [
            (Self.supportThreadsPageOneData, 200, 0),
            (Self.supportThreadsPageTwoData, 200, 0),
            (Self.errorData, 500, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/my/support-threads/thread-2"] = (Self.errorData, 500)
        let client = try makeClient()
        let viewModel = NativeSupportViewModel(client: client, routeMatch: nil)

        await viewModel.load()
        await viewModel.loadMoreThreads()
        await viewModel.selectThread(id: "thread-2")

        XCTAssertEqual(viewModel.threads.map(\.id), ["thread-1", "thread-2"])
        XCTAssertTrue(viewModel.canLoadMoreThreads)
        XCTAssertNotNil(viewModel.detailErrorMessage)
        XCTAssertTrue(viewModel.selectedThreadMessages.isEmpty)

        await viewModel.startNewThread()

        XCTAssertNil(viewModel.selectedThreadId)
        XCTAssertEqual(viewModel.subject, "")
        XCTAssertEqual(viewModel.message, "")
        XCTAssertEqual(viewModel.conversationId, "")

        await viewModel.loadMoreThreads()

        XCTAssertNotNil(viewModel.listErrorMessage)
        XCTAssertFalse(viewModel.isLoadingList)
    }

    func testSupportViewModelLoadsMoreDetailMessagesWithAfterCursor() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/support-threads/thread-1"] = [
            (Self.supportThreadDetailPageOneData, 200, 0),
            (Self.supportThreadDetailPageTwoData, 200, 0)
        ]
        let client = try makeClient()
        let viewModel = NativeSupportViewModel(client: client, routeMatch: nil)

        await viewModel.selectThread(id: "thread-1")
        await viewModel.loadMoreSelectedThreadMessages()

        XCTAssertEqual(viewModel.selectedThreadMessages.map(\.id), ["support-message-0", "support-message-1"])
        XCTAssertFalse(viewModel.canLoadMoreSelectedThreadMessages)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/my/support-threads/thread-1",
            "/api/v1/my/support-threads/thread-1"
        ])
        XCTAssertEqual(
            try URLComponents(url: XCTUnwrap(CannedFeedURLProtocol.capturedURLs.last), resolvingAgainstBaseURL: false)?
                .queryItems?
                .sorted(by: { $0.name < $1.name }),
            [
                URLQueryItem(name: "after", value: "cursor-1"),
                URLQueryItem(name: "limit", value: "25")
            ]
        )
    }

    func testSupportViewModelRejectsBlankCreateSubject() async throws {
        let viewModel = try NativeSupportViewModel(client: makeClient(), routeMatch: nil)

        await viewModel.createThread()

        XCTAssertEqual(uiEnglish(viewModel.createErrorMessage), "Subject is required.")
        XCTAssertFalse(viewModel.isCreating)
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
    }

    func testChatViewModelLoadsMutatesAndDeletesConversations() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/conversations"] = [
            (Self.chatConversationListPageOneData, 200, 0),
            (Self.chatConversationListPageTwoData, 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/conversations/conversation-1/messages"] = [
            (Self.chatMessagesData, 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/my/conversations/conversation-1"] = (Self.renamedConversationData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/conversations/conversation-1/title"] = (
            Self.generatedConversationData,
            200
        )
        let client = try makeClient()
        let viewModel = NativeChatViewModel(client: client, routeMatch: nil)
        viewModel.titleProviderSelection = .openAI

        await viewModel.load()
        await viewModel.loadMoreConversations()
        await viewModel.selectConversation(id: "conversation-1")
        viewModel.conversationTitleDraft = "Renamed chat"
        await viewModel.renameSelectedConversation()
        viewModel.conversationTitleDraft = ""
        await viewModel.generateTitleIfNeeded(conversationId: "conversation-1")
        await viewModel.selectConversation(id: "conversation-1")
        XCTAssertNil(viewModel.detailErrorMessage)
        await viewModel.startNewConversation()
        viewModel.selectedConversationId = "conversation-1"
        viewModel.messages = [.init(id: "message-local", role: .assistant, content: "Cached")]
        await viewModel.deleteSelectedConversation()

        XCTAssertEqual(viewModel.conversations.map(\.id), ["conversation-2"])
        XCTAssertNil(viewModel.selectedConversationId)
        XCTAssertEqual(viewModel.draftMessage, "")
        XCTAssertTrue(viewModel.messages.isEmpty)
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, [
            "GET",
            "GET",
            "GET",
            "PATCH",
            "POST",
            "DELETE"
        ])
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies
            .contains { $0?.contains(#""title":"Renamed chat""#) == true })
    }

    func testChatViewModelDeletesDeepLinkedConversationMissingFromList() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/conversations/conversation-9"] = (Data("{}".utf8), 204)
        let client = try makeClient()
        let viewModel = NativeChatViewModel(client: client, routeMatch: nil)
        viewModel.selectedConversationId = "conversation-9"
        viewModel.messages = [.init(id: "message-local", role: .assistant, content: "Cached")]
        viewModel.conversationTitleDraft = "Deep link"

        await viewModel.deleteSelectedConversation()

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["DELETE"])
        XCTAssertNil(viewModel.selectedConversationId)
        XCTAssertTrue(viewModel.messages.isEmpty)
        XCTAssertEqual(viewModel.conversationTitleDraft, "")
    }

    func testChatViewModelSendsDraftMessageAndSurfacesStreamFailure() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/conversations"] = [
            (Self.createdConversationData, 201, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/conversations/conversation-3/chat"] = (Self.errorData, 500)
        let client = try makeClient()
        let viewModel = NativeChatViewModel(client: client, routeMatch: nil)
        viewModel.titleProviderSelection = .openAI
        viewModel.draftMessage = "  Hello support  "

        await viewModel.sendDraftMessage()

        XCTAssertEqual(viewModel.selectedConversationId, "conversation-3")
        XCTAssertEqual(viewModel.conversations.first?.id, "conversation-3")
        XCTAssertEqual(viewModel.draftMessage, "")
        XCTAssertTrue(viewModel.messages.isEmpty)
        XCTAssertNotNil(viewModel.streamErrorMessage)
        XCTAssertFalse(viewModel.isStreaming)
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["POST", "POST"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedBodies.first, "{}")
        XCTAssertEqual(CannedFeedURLProtocol.capturedBodies.last??.contains(#""message":"Hello support""#), true)
    }

    func testChatViewModelGuardsBlankSendAndRollsBackFailures() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/conversations/conversation-1"] = (Self.errorData, 500)
        let client = try makeClient()
        let viewModel = NativeChatViewModel(client: client, routeMatch: nil)
        let originalConversation = try Self.decode(
            ChatConversation.self,
            #"{"id":"conversation-1","title":"Original title","created_at":"2026-01-01T00:00:00Z","created_by_id":"user-1","updated_at":"2026-01-01T00:01:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null}"#
        )
        viewModel.conversations = [originalConversation]
        viewModel.selectedConversationId = "conversation-1"
        viewModel.conversationTitleDraft = "Renamed title"
        viewModel.draftMessage = "   "

        await viewModel.sendDraftMessage()
        await viewModel.renameSelectedConversation()
        viewModel.messages = [.init(id: "message-1", role: .assistant, content: "Cached")]
        await viewModel.deleteSelectedConversation()

        XCTAssertEqual(viewModel.conversations.first?.title, "Original title")
        XCTAssertEqual(viewModel.conversationTitleDraft, "Original title")
        XCTAssertEqual(viewModel.selectedConversationId, "conversation-1")
        XCTAssertEqual(viewModel.messages.first?.content, "Cached")
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["PATCH", "DELETE"])
    }

    func testChatViewModelRouteMatchLoadsInitialConversation() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/conversations"] = (Self.chatConversationListPageOneData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/conversations/conversation-1/messages"] = (
            Self.chatMessagesData,
            200
        )
        let client = try makeClient()
        let viewModel = NativeChatViewModel(
            client: client,
            routeMatch: NativeRouteMatch(
                path: "/app/chat/conversation-1",
                template: "/app/chat/:id",
                params: ["id": "conversation-1"]
            )
        )

        await viewModel.load()

        XCTAssertEqual(viewModel.selectedConversationId, "conversation-1")
        XCTAssertEqual(viewModel.messages.first?.content, "Hello")
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["GET", "GET"])
    }

    func testChatViewModelDeepLinkLoadsConversationFromLaterPageBeforeDetail() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/conversations"] = [
            (Self.chatConversationListPageOneData, 200, 0),
            (Self.chatConversationListPageTwoData, 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/my/conversations/conversation-2/messages"] = (
            Data(#"{"results":[],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}"#.utf8),
            200
        )
        let client = try makeClient()
        let viewModel = NativeChatViewModel(
            client: client,
            routeMatch: NativeRouteMatch(
                path: "/app/chat/conversation-2",
                template: "/app/chat/:id",
                params: ["id": "conversation-2"]
            )
        )

        await viewModel.load()
        await viewModel.generateTitleIfNeeded(conversationId: "conversation-2")

        XCTAssertEqual(viewModel.selectedConversationId, "conversation-2")
        XCTAssertEqual(viewModel.selectedConversationTitle, .verbatim("Second chat"))
        XCTAssertEqual(viewModel.conversationTitleDraft, "Second chat")
        XCTAssertEqual(viewModel.conversations.map(\.id), ["conversation-1", "conversation-2"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["GET", "GET", "GET"])
    }

    func testChatMessageBubbleRendersMetadataAndErrorSections() throws {
        let message = NativeChatTimelineMessage(
            id: "message-1",
            role: .assistant,
            content: "",
            isStreaming: true,
            toolCalls: [.init(toolCallId: "tool-1", name: "search", arguments: #"{"q":"voucha"}"#)],
            toolResults: [.init(toolCallId: "tool-1", result: .string("Done"))],
            subagentSteps: [.init(agentName: "helper", toolName: "search", toolCallId: "tool-1")],
            subagentTextChunks: [.init(agentName: "helper", toolCallId: "tool-1", content: "Child output")],
            error: .verbatim("Something failed")
        )
        let inspection = try NativeChatMessageBubbleView(message: message).inspect()

        XCTAssertEqual(try inspection.find(text: "Assistant").string(), "Assistant")
        XCTAssertEqual(try inspection.find(text: "Tool calls").string(), "Tool calls")
        XCTAssertEqual(try inspection.find(text: #"search: {"q":"voucha"}"#).string(), #"search: {"q":"voucha"}"#)
        XCTAssertEqual(try inspection.find(text: "Tool results").string(), "Tool results")
        XCTAssertEqual(try inspection.find(text: "tool-1: Done").string(), "tool-1: Done")
        XCTAssertEqual(try inspection.find(text: "Subagent steps").string(), "Subagent steps")
        XCTAssertEqual(try inspection.find(text: "helper • search").string(), "helper • search")
        XCTAssertEqual(try inspection.find(text: "Subagent text").string(), "Subagent text")
        XCTAssertEqual(try inspection.find(text: "helper: Child output").string(), "helper: Child output")
        XCTAssertEqual(try inspection.find(text: "Something failed").string(), "Something failed")
    }

    func testChatMessageBubbleRendersUserMessages() throws {
        let message = NativeChatTimelineMessage(
            id: "message-1",
            role: .user,
            content: "Hello",
            isStreaming: false
        )
        let inspection = try NativeChatMessageBubbleView(message: message).inspect()

        XCTAssertEqual(try inspection.find(text: "You").string(), "You")
        XCTAssertEqual(try inspection.find(text: "Hello").string(), "Hello")
    }

    func testChatSurfaceRendersNativeComposerAndSidebar() throws {
        let sut = NativeChatSurface(client: nil, routeMatch: nil)
        let inspection = try sut.inspect()

        let chatsText = try inspection.find(text: "Chats").string()
        XCTAssertEqual(chatsText, "Chats")
        _ = try inspection.find(button: "Send")
        _ = try inspection.find(button: "Stop")
    }

    func testSupportSurfaceRendersNativeCreationForm() throws {
        let sut = NativeSupportSurface(client: nil, routeMatch: nil)
        let inspection = try sut.inspect()

        let supportText = try inspection.find(text: "Support").string()
        XCTAssertEqual(supportText, "Support")
        _ = try inspection.find(button: "Create request")
        _ = try inspection.find(text: "Conversation ID")
    }

    func testChatDetailAndSidebarRenderLoadedState() throws {
        let viewModel = NativeChatViewModel(client: nil, routeMatch: nil)
        viewModel.conversations = try [
            Self.decode(
                ChatConversation.self,
                #"{"id":"conversation-1","title":"","created_at":"2026-01-01T00:00:00Z","created_by_id":"user-1","updated_at":"2026-01-01T00:01:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null}"#
            ),
            Self.decode(
                ChatConversation.self,
                #"{"id":"conversation-2","title":"Named chat","created_at":"2026-01-01T00:00:00Z","created_by_id":"user-1","updated_at":"2026-01-01T00:02:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null}"#
            )
        ]
        viewModel.listPageInfo = .init(hasNextPage: true, endCursor: "cursor-1")
        viewModel.selectedConversationId = "conversation-2"
        viewModel.conversationTitleDraft = ""
        viewModel.detailErrorMessage = .verbatim("Unable to load messages")
        viewModel.listErrorMessage = .verbatim("Unable to load chats")
        viewModel.isGeneratingTitle = true
        viewModel.messages = [
            .init(id: "message-1", role: .assistant, content: "Hello", isStreaming: false)
        ]
        let surface = NativeChatSurface(client: nil, routeMatch: nil)

        let detail = try surface.chatDetail(viewModel: viewModel).inspect()
        let sidebar = try surface.chatSidebar(viewModel: viewModel).inspect()

        XCTAssertEqual(try detail.find(text: "Named chat").string(), "Named chat")
        XCTAssertEqual(try detail.find(text: "Unable to load messages").string(), "Unable to load messages")
        _ = try detail.find(button: "Generate title")
        _ = try detail.find(button: "Rename")
        _ = try detail.find(button: "Delete")
        XCTAssertEqual(try sidebar.find(text: "New conversation").string(), "New conversation")
        XCTAssertEqual(try sidebar.find(text: "Named chat").string(), "Named chat")
        XCTAssertEqual(try sidebar.find(text: "Unable to load chats").string(), "Unable to load chats")
        _ = try sidebar.find(button: "Load more")
    }

    func testChatDetailDisablesSendWhileLoadingDetail() throws {
        let viewModel = NativeChatViewModel(client: nil, routeMatch: nil)
        viewModel.draftMessage = "Hello support"
        viewModel.isLoadingDetail = true

        let detail = try NativeChatSurface(client: nil, routeMatch: nil)
            .chatDetail(viewModel: viewModel)
            .inspect()

        XCTAssertTrue(try detail.find(button: "Send").isDisabled())
    }

    func testSupportDetailAndSidebarRenderLoadedState() throws {
        let viewModel = NativeSupportViewModel(client: nil, routeMatch: nil)
        viewModel.threads = try [
            Self.decode(
                SupportThread.self,
                #"{"id":"thread-1","support_contact_id":"contact-1","subject":"Need help","conversation_id":"conversation-1","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:01:00Z","assigned_at":null,"assigned_to_id":null,"resolved_at":null,"resolved_by_id":null,"status":"open","contact_user_id":"user-1"}"#
            )
        ]
        viewModel.selectedThreadId = "thread-1"
        viewModel.selectedThreadPageInfo = .init(hasNextPage: true, endCursor: "cursor-1")
        viewModel.detailErrorMessage = .verbatim("Unable to load thread")
        viewModel.listErrorMessage = .verbatim("Unable to load support")
        viewModel.selectedThreadMessages = try [
            Self.decode(
                SupportMessage.self,
                #"{"id":"support-message-1","support_thread_id":"thread-1","direction":"outbound","body_text":"Team reply","body_html":"<p>Team reply</p>","created_at":"2026-01-01T00:02:00Z","created_by_id":"user-2","updated_at":"2026-01-01T00:02:00Z","email_message_id":null,"email_subject":null,"email_from":null,"email_to":null,"drafted_at":null,"edited_at":null,"edited_by_id":null,"approved_at":null,"approved_by_id":null,"sent_at":null}"#
            )
        ]
        let surface = NativeSupportSurface(client: nil, routeMatch: nil)

        let detail = try surface.supportDetail(viewModel: viewModel).inspect()

        XCTAssertEqual(try detail.find(text: "Need help").string(), "Need help")
        XCTAssertEqual(try detail.find(text: "Open").string(), "Open")
        XCTAssertEqual(try detail.find(text: "Unable to load thread").string(), "Unable to load thread")
        XCTAssertEqual(try detail.find(text: "Support team").string(), "Support team")
        XCTAssertEqual(try detail.find(text: "Team reply").string(), "Team reply")
    }
}
