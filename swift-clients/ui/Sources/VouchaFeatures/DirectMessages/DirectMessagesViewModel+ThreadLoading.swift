import VouchaAPI
import VouchaModels

@MainActor
extension DirectMessagesViewModel {
    func clearSelectedConversation() {
        selectedConversationId = nil
        messages = []
        participants = []
        clearParticipantSearch()
        participantAddPolicy = .ownerOnly
        messageCursor = nil
        hasMoreMessages = true
        threadState = .idle
    }

    public func loadThread(conversationId: String) async {
        threadLoadGeneration += 1
        let generation = threadLoadGeneration
        selectedConversationId = conversationId
        messages = []
        participants = []
        clearParticipantSearch()
        participantAddPolicy = .ownerOnly
        messageCursor = nil
        hasMoreMessages = true
        await loadThreadData(conversationId: conversationId, generation: generation)
    }

    public func loadMoreMessages() async {
        guard let selectedConversationId, hasMoreMessages, let cursor = messageCursor else { return }
        if case .loading = threadState {
            return
        }
        let generation = threadLoadGeneration
        let requestKey = "\(generation)|\(selectedConversationId)|\(cursor)"
        guard !loadingMoreMessageRequestKeys.contains(requestKey) else { return }
        loadingMoreMessageRequestKeys.insert(requestKey)
        defer { loadingMoreMessageRequestKeys.remove(requestKey) }
        await loadMessagePage(conversationId: selectedConversationId, cursor: cursor, generation: generation)
    }

    func loadThreadData(conversationId: String, generation: Int) async {
        threadState = .loading
        async let messageLoad: Void = loadMessagePage(
            conversationId: conversationId,
            cursor: nil,
            generation: generation
        )
        async let participantLoad: Void = loadParticipants(conversationId: conversationId, generation: generation)
        async let conversationLoad = loadConversation(conversationId: conversationId, generation: generation)
        _ = await (messageLoad, participantLoad, conversationLoad)
        guard selectedConversationId == conversationId, threadLoadGeneration == generation else { return }
        if case .error = threadState {
            return
        }
        threadState = .loaded
    }

    func loadMessagePage(conversationId: String, cursor: String?, generation: Int) async {
        do {
            let page: Page<DirectMessage> = try await client.send(.myMessageConversationMessages(
                conversationId: conversationId,
                after: cursor
            ))
            guard selectedConversationId == conversationId,
                  threadLoadGeneration == generation,
                  messageCursor == cursor
            else { return }
            if cursor == nil {
                messages = page.results
            } else {
                var seenIds = Set(messages.map(\.id))
                let olderMessages = page.results.filter { seenIds.insert($0.id).inserted }
                messages = olderMessages + messages
            }
            messageCursor = page.pageInfo.endCursor
            hasMoreMessages = page.pageInfo.hasNextPage
        } catch {
            guard selectedConversationId == conversationId,
                  threadLoadGeneration == generation,
                  messageCursor == cursor
            else { return }
            if cursor == nil {
                threadState = .error(vouchaError(from: error))
            }
        }
    }

    func loadParticipants(conversationId: String, generation: Int) async {
        do {
            let page: Page<ConversationParticipant> = try await client.send(.myMessageConversationParticipants(
                conversationId: conversationId
            ))
            guard selectedConversationId == conversationId, threadLoadGeneration == generation else { return }
            participants = page.results
        } catch {
            guard selectedConversationId == conversationId, threadLoadGeneration == generation else { return }
            threadState = .error(vouchaError(from: error))
        }
    }

    func loadConversation(conversationId: String, generation: Int) async {
        do {
            let response: DirectConversationResponse = try await client.send(.myMessageConversation(
                conversationId: conversationId
            ))
            guard selectedConversationId == conversationId, threadLoadGeneration == generation else { return }
            participantAddPolicy = response.conversation.participantAddPolicy ?? .ownerOnly
        } catch {
            guard selectedConversationId == conversationId, threadLoadGeneration == generation else { return }
            threadState = .error(vouchaError(from: error))
        }
    }
}
