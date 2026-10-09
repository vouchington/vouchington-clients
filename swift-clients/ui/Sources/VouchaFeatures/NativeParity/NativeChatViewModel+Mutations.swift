import Foundation
import VouchaAPI
import VouchaLocalization
import VouchaModels

extension NativeChatViewModel {
    func sendDraftMessage() async {
        guard let text = normalizedDraftMessage else { return }
        guard let client else { return }
        guard isLoadingDetail == false else { return }
        let providerSelection = titleProviderSelection
        let provider = titleProviderResolver.provider(for: providerSelection)
        let retry = pendingTurn(for: text, providerSelection: providerSelection, provider: provider)
        guard retry != nil || provider.status.isAvailable else {
            detailErrorMessage = nil
            streamErrorMessage = provider.status.detail ?? .message(.nativeSwiftChatOnDeviceUnavailable)
            return
        }

        detailErrorMessage = nil
        streamErrorMessage = nil
        draftMessage = ""
        let preparedConversation: (conversationId: String, created: Bool)?
        do {
            preparedConversation = try await ensureConversationForDraftSend(client: client)
        } catch {
            detailErrorMessage = .message(.nativeSwiftChatUnableToCreateConversation)
            return
        }

        guard let conversationId = preparedConversation?.conversationId else { return }
        if preparedConversation?.created == true {
            createdConversationIds.insert(conversationId)
        }

        streamTask?.cancel()
        let localTurnIds = NativeChatMessageIDs.nextLocalTurn()
        let userMessageId = retry?.context.userMessageId ?? localTurnIds.user
        messages.append(.init(
            id: userMessageId,
            role: .user,
            content: text,
            isStreaming: false
        ))
        beginStreaming(conversationId: conversationId, userMessageId: userMessageId)
        pendingLocalTurn = retry

        streamTask = Task { [weak self, client] in
            await self?.streamDraftMessage(
                client: client,
                context: retry?.context ?? .init(
                    conversationId: conversationId,
                    text: text,
                    userMessageId: userMessageId,
                    assistantMessageId: localTurnIds.assistant,
                    createdConversation: preparedConversation?.created == true,
                    providerSelection: providerSelection
                )
            )
        }
        _ = await streamTask?.value
    }

    func abortStreaming() {
        let abortedUserMessageId = streamingUserMessageId
        let abortedAssistantMessageId = streamingAssistantMessageId
        streamTask?.cancel()
        if abortedAssistantMessageId?.hasPrefix("local-assistant-") == true ||
            abortedAssistantMessageId == pendingLocalAssistantMessageId && abortedAssistantMessageId != nil {
            if let abortedUserMessageId {
                discardConversationMessage(id: abortedUserMessageId)
            }
            if let abortedAssistantMessageId {
                discardConversationMessage(id: abortedAssistantMessageId)
            }
        } else {
            updateStreamingAssistantMessage(isStreaming: false)
        }
        isStreaming = false
        streamTask = nil
        streamingConversationId = nil
        streamingUserMessageId = nil
        streamingAssistantMessageId = nil
        pendingLocalAssistantMessageId = nil
    }

    func renameSelectedConversation() async {
        guard let client, let id = selectedConversationId else { return }
        let title = conversationTitleDraft.trimmingCharacters(in: .whitespacesAndNewlines)
        guard title.isEmpty == false else { return }

        let previous = selectedConversation
        updateConversation(id: id) { $0.title = title }
        do {
            let response: ChatConversationResponse = try await client.send(
                .renameConversation(conversationId: id, title: title)
            )
            updateConversation(id: id) { $0 = response.conversation }
            if selectedConversationId == id {
                conversationTitleDraft = response.conversation.title
            }
        } catch {
            if let previous {
                updateConversation(id: id) { $0 = previous }
                if selectedConversationId == id {
                    conversationTitleDraft = previous.title
                }
            }
        }
    }

    func deleteSelectedConversation() async {
        guard let client, let id = selectedConversationId else { return }
        let selectionRevisionAtDeleteStart = selectionRevision
        if streamingConversationId == id {
            abortStreaming()
        }
        let index = conversations.firstIndex(where: { $0.id == id })
        let removed = index.map { conversations.remove(at: $0) }
        let removedMessages = messages
        selectedConversationId = nil
        messages = []
        do {
            let _: EmptyResponse = try await client.send(
                .deleteConversation(conversationId: id)
            )
            if selectedConversationId == nil, selectionRevision == selectionRevisionAtDeleteStart {
                conversationTitleDraft = ""
            }
        } catch {
            if let removed, let index {
                conversations.insert(removed, at: index)
            }
            if selectedConversationId == nil, selectionRevision == selectionRevisionAtDeleteStart {
                selectedConversationId = id
                messages = removedMessages
                conversationTitleDraft = removed?.title ?? conversationTitleDraft
            }
        }
    }

    func streamDraftMessage(
        client: APIClient,
        context: NativeChatDraftSendContext
    ) async {
        let provider = titleProviderResolver.provider(for: context.providerSelection)
        guard pendingLocalTurn?.context.userMessageId == context.userMessageId || provider.status.isAvailable else {
            failUnpersistedLocalGeneration(
                context: context,
                assistantMessageId: nil,
                message: provider.status.detail ?? .message(.nativeSwiftChatOnDeviceUnavailable)
            )
            return
        }
        await generateLocalDraftMessage(client: client, provider: provider, context: context)
    }
}
