import Foundation
import VouchaAPI
import VouchaModels

@MainActor
public extension DirectMessagesViewModel {
    func createConversation(userIds: [String], text: String) async -> CreateConversationResult {
        let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !userIds.isEmpty, !trimmed.isEmpty else { return .ignored }
        guard !isCreatingConversation else { return .ignored }
        isCreatingConversation = true
        defer { isCreatingConversation = false }
        state = .loading
        var createdConversationId: String?
        do {
            let response: DirectConversationResponse = try await client
                .send(.createMyMessageConversation(userIds: userIds))
            selectedConversationId = response.conversation.id
            createdConversationId = response.conversation.id
            let message: DirectMessageResponse = try await client.send(.sendMyMessageConversationMessage(
                conversationId: response.conversation.id,
                text: trimmed
            ))
            let createdMessage = message.message.withLocalSenderLabel(currentUserId: currentUserId)
            clearParticipantSearch()
            await reloadInbox()
            if shouldUpsertConversationFallback(conversationId: response.conversation.id) {
                await upsertConversationFallbackInInbox(response.conversation)
            }
            guard selectedConversationId == response.conversation.id else {
                return .created
            }
            messages = [createdMessage]
            messageCursor = nil
            hasMoreMessages = true
            threadLoadGeneration += 1
            await loadThreadData(conversationId: response.conversation.id, generation: threadLoadGeneration)
            if case .error = state {
                return .created
            }
            state = .loaded
            return .created
        } catch {
            if let createdConversationId,
               selectedConversationId == createdConversationId {
                await loadCreatedConversationThreadAfterSendFailure(conversationId: createdConversationId)
            }
            state = .error(vouchaError(from: error))
            return .failed(createdConversationId: createdConversationId)
        }
    }

    func sendMessage(text: String) async -> Bool {
        guard let requestConversationId = selectedConversationId else { return false }
        let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return false }
        if case .loading = threadState {
            return false
        }
        let optimistic = DirectMessage.optimistic(
            conversationId: requestConversationId,
            bodyText: trimmed,
            createdById: currentUserId
        )
        messages.append(optimistic)
        do {
            let response: DirectMessageResponse = try await client.send(.sendMyMessageConversationMessage(
                conversationId: requestConversationId,
                text: trimmed
            ))
            if selectedConversationId == requestConversationId {
                let message = response.message.withLocalSenderLabel(currentUserId: currentUserId)
                if let optimisticIndex = messages.firstIndex(where: { $0.id == optimistic.id }) {
                    messages[optimisticIndex] = message
                } else if !messages.contains(where: { $0.id == message.id }) {
                    messages.append(message)
                }
            }
            await reloadInbox()
            if shouldUpsertConversationFallback(conversationId: requestConversationId),
               let conversation = try? await loadConversationFallback(conversationId: requestConversationId) {
                upsertConversationFallbackRowInInbox(conversation)
            }
            if selectedConversationId == requestConversationId {
                threadState = .loaded
            }
            return true
        } catch {
            guard selectedConversationId == requestConversationId else { return false }
            messages.removeAll { $0.id == optimistic.id }
            threadState = .error(vouchaError(from: error))
            return false
        }
    }
}

private extension DirectMessagesViewModel {
    func loadCreatedConversationThreadAfterSendFailure(conversationId: String) async {
        threadLoadGeneration += 1
        let generation = threadLoadGeneration
        if let conversation = try? await loadConversationFallback(conversationId: conversationId) {
            upsertConversationFallbackRowInInbox(conversation)
        }
        guard selectedConversationId == conversationId else { return }
        await loadThreadData(conversationId: conversationId, generation: generation)
    }

    func shouldUpsertConversationFallback(conversationId: String) -> Bool {
        if case .error = state {
            return true
        }
        return !conversations.contains(where: { $0.id == conversationId })
    }

    func upsertConversationFallbackInInbox(_ conversation: DirectConversation) async {
        if let hydrated = try? await loadConversationFallback(conversationId: conversation.id) {
            upsertConversationFallbackRowInInbox(hydrated)
            return
        }
        upsertConversationFallbackRowInInbox(conversation)
    }

    func upsertConversationFallbackRowInInbox(_ conversation: DirectConversation) {
        conversations.removeAll { $0.id == conversation.id }
        conversations.insert(conversation, at: 0)
    }

    func loadConversationFallback(conversationId: String) async throws -> DirectConversation {
        async let conversationResponse: DirectConversationResponse = client.send(.myMessageConversation(
            conversationId: conversationId
        ))
        async let participantPage: Page<ConversationParticipant> = client.send(.myMessageConversationParticipants(
            conversationId: conversationId
        ))
        let (conversation, participants) = try await (conversationResponse.conversation, participantPage.results)
        let participantUsernames = participants
            .filter { $0.userId != currentUserId }
            .compactMap(\.username)
            .filter { !$0.isEmpty }
        return conversation.withParticipantUsernames(
            participantUsernames.isEmpty ? conversation.participantUsernames : participantUsernames
        )
    }
}

private extension DirectMessage {
    func withLocalSenderLabel(currentUserId: String?) -> DirectMessage {
        guard senderUsername == nil, createdById == currentUserId else {
            return self
        }
        return DirectMessage(
            id: id,
            conversationId: conversationId,
            bodyText: bodyText,
            createdById: createdById,
            senderUsername: "You",
            createdAt: createdAt,
            updatedAt: updatedAt,
            deletedAt: deletedAt
        )
    }
}

public enum CreateConversationResult: Equatable, Sendable {
    case created
    case ignored
    case failed(createdConversationId: String?)
}
