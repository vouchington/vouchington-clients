import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

extension NativeChatViewModel {
    func loadConversation(id: String, revision: Int) async {
        guard let client else { return }
        guard activeConversationDetailLoadRevision == revision else { return }
        isLoadingDetail = true
        detailErrorMessage = nil
        defer {
            if activeConversationDetailLoadRevision == revision {
                isLoadingDetail = false
                activeConversationDetailLoadRevision = 0
            }
        }

        do {
            let response: ChatMessagesResponse = try await client.send(.myConversationMessages(conversationId: id))
            applyConversationDetailResponse(response, id: id, revision: revision)
            await hydrateConversationMetadataIfNeeded(id: id, revision: revision)
        } catch let error as VouchaError {
            guard activeConversationDetailLoadRevision == revision else { return }
            applyConversationDetailError(error.errorDescription, id: id, revision: revision)
        } catch {
            guard activeConversationDetailLoadRevision == revision else { return }
            applyConversationDetailError(error.localizedDescription, id: id, revision: revision)
        }
    }

    func isCurrentConversationDetailLoad(id: String, revision: Int) -> Bool {
        activeConversationDetailLoadRevision == revision && selectedConversationId == id
    }

    func applyConversationDetailResponse(_ response: ChatMessagesResponse, id: String, revision: Int) {
        guard isCurrentConversationDetailLoad(id: id, revision: revision) else { return }
        messages = response.results.map { makeTimelineMessage(from: $0) }
        detailPageInfo = .init(
            hasNextPage: response.pageInfo.hasNextPage,
            endCursor: response.pageInfo.endCursor
        )
        loadedConversationDetailId = id
        if let conversation = conversations.first(where: { $0.id == id }) {
            conversationTitleDraft = conversation.title
        }
    }

    func applyConversationDetailError(_ message: String?, id: String, revision: Int) {
        guard isCurrentConversationDetailLoad(id: id, revision: revision) else { return }
        detailErrorMessage = message.map(UiVerbatimText.verbatim)
        messages = []
        detailPageInfo = nil
    }

    func ensureConversationLoaded(id: String, maximumPageLoads: Int = 10) async {
        guard conversations.contains(where: { $0.id == id }) == false else { return }
        guard client != nil else { return }

        var remainingPageLoads = maximumPageLoads
        while remainingPageLoads > 0,
              conversations.contains(where: { $0.id == id }) == false,
              canLoadMoreConversations {
            remainingPageLoads -= 1
            let previousCount = conversations.count
            await loadMoreConversations()
            guard conversations.count > previousCount else { break }
        }
    }

    func createConversationForSend(client: APIClient) async throws -> ChatConversation {
        let response: ChatConversationResponse = try await client.send(.createConversation(title: nil))
        return response.conversation
    }

    func beginStreaming(conversationId: String) {
        streamingConversationId = conversationId
        streamingAssistantMessageId = nil
        pendingLocalAssistantMessageId = nil
        isStreaming = true
        streamedContent = ""
        toolCalls = []
        toolResults = []
        subagentSteps = []
        subagentTextChunks = []
    }

    func beginStreaming(conversationId: String, userMessageId: String) {
        streamingUserMessageId = userMessageId
        beginStreaming(conversationId: conversationId)
    }

    @discardableResult
    func finishStreaming() -> Bool {
        guard streamingConversationId != nil else { return false }
        updateStreamingAssistantMessage(isStreaming: false)
        isStreaming = false
        streamingConversationId = nil
        streamingUserMessageId = nil
        streamingAssistantMessageId = nil
        pendingLocalAssistantMessageId = nil
        streamedContent = ""
        toolCalls = []
        toolResults = []
        subagentSteps = []
        subagentTextChunks = []
        streamTask = nil
        return true
    }

    func failStreaming(message: String) {
        failStreaming(message: .verbatim(message))
    }

    func failStreaming(message: UiVerbatimText) {
        streamErrorMessage = message
        updateStreamingAssistantMessage(error: message, isStreaming: false)
        isStreaming = false
        streamingConversationId = nil
        streamingUserMessageId = nil
        streamingAssistantMessageId = nil
        pendingLocalAssistantMessageId = nil
        streamTask = nil
    }

    func ensureAssistantMessage(id: String) {
        streamingAssistantMessageId = id
        if messages.contains(where: { $0.id == id }) == false {
            messages.append(.init(id: id, role: .assistant, content: "", isStreaming: true))
        }
        updateStreamingAssistantMessage()
    }

    func updateStreamingAssistantMessage(error: UiVerbatimText? = nil, isStreaming: Bool = true) {
        guard let id = streamingAssistantMessageId else { return }
        updateConversationMessage(id: id) { message in
            message.role = .assistant
            message.content = streamedContent
            message.isStreaming = isStreaming
            message.toolCalls = toolCalls
            message.toolResults = toolResults
            message.subagentSteps = subagentSteps
            message.subagentTextChunks = subagentTextChunks
            message.error = error
        }
    }

}
