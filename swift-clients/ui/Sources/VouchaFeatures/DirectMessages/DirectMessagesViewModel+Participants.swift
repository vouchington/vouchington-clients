import VouchaAPI
import VouchaModels

extension DirectMessagesViewModel {
    func addParticipant(userId: String) async {
        guard let conversationId = selectedConversationId, !userId.isEmpty else { return }
        do {
            let response: ConversationParticipantResponse = try await client.send(
                .addMyMessageConversationParticipant(conversationId: conversationId, userId: userId)
            )
            guard selectedConversationId == conversationId else { return }
            participants.append(response.participant)
            await reloadConversationInInbox(conversationId: conversationId)
        } catch {
            guard selectedConversationId == conversationId else { return }
            threadState = .error(vouchaError(from: error))
        }
    }

    func removeParticipant(userId: String) async {
        guard let conversationId = selectedConversationId, !userId.isEmpty else { return }
        do {
            let _: EmptyResponse = try await client.send(
                .removeMyMessageConversationParticipant(conversationId: conversationId, userId: userId)
            )
            guard selectedConversationId == conversationId else { return }
            if userId == currentUserId {
                clearSelectedConversation()
                removeConversationFromInbox(conversationId: conversationId)
                return
            }
            participants.removeAll { $0.userId == userId }
            await reloadConversationInInbox(conversationId: conversationId)
        } catch {
            guard selectedConversationId == conversationId else { return }
            threadState = .error(vouchaError(from: error))
        }
    }

    func updatePolicy(_ policy: ConversationParticipantAddPolicy) async {
        guard let conversationId = selectedConversationId else { return }
        do {
            let response: ConversationParticipantPolicyResponse = try await client.send(
                .updateMyMessageConversationParticipantAddPolicy(
                    conversationId: conversationId,
                    policy: policy
                )
            )
            guard selectedConversationId == conversationId else { return }
            participantAddPolicy = response.participantAddPolicy
        } catch {
            guard selectedConversationId == conversationId else { return }
            threadState = .error(vouchaError(from: error))
        }
    }
}

private extension DirectMessagesViewModel {
    func reloadConversationInInbox(conversationId: String) async {
        do {
            let response: DirectConversationResponse = try await client.send(.myMessageConversation(
                conversationId: conversationId
            ))
            let participantPage: Page<ConversationParticipant> = try await client
                .send(.myMessageConversationParticipants(
                    conversationId: conversationId
                ))
            guard selectedConversationId == conversationId else { return }
            participantAddPolicy = response.conversation.participantAddPolicy ?? .ownerOnly
            let participantUsernames = participantPage.results
                .filter { $0.userId != currentUserId }
                .compactMap(\.username)
                .filter { !$0.isEmpty }
            let conversation = response.conversation.withParticipantUsernames(
                participantUsernames.isEmpty ? response.conversation.participantUsernames : participantUsernames
            )
            if let index = conversations.firstIndex(where: { $0.id == conversationId }) {
                conversations[index] = conversation
            } else {
                conversations.insert(conversation, at: 0)
            }
            conversations.sort { $0.updatedAt > $1.updatedAt }
        } catch {
            guard selectedConversationId == conversationId else { return }
            threadState = .error(vouchaError(from: error))
        }
    }
}

extension DirectConversation {
    func withParticipantUsernames(_ participantUsernames: [String]?) -> DirectConversation {
        var updated = self
        updated.participantUsernames = participantUsernames
        return updated
    }
}
