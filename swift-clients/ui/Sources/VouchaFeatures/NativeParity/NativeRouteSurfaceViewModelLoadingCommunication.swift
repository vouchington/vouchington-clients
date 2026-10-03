import VouchaAPI
import VouchaLocalization
import VouchaModels

extension NativeRouteSurfaceViewModel {
    func loadMessageRows(for destination: NativeRouteDestinationIdentifier, client: APIClient) async throws
        -> [NativeRouteDestinationRow] {
        switch destination {
        case .chat:
            return try await loadChatRows(client: client)
        case .messages:
            return try await loadDirectMessageRows(client: client)
        default:
            return []
        }
    }

    private func loadChatRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        if let conversationId = chatConversationId {
            return try await loadChatDetailRows(client: client, conversationId: conversationId)
        }
        let page: ChatConversationListResponse = try await client.send(.myConversations(limit: 50))
        return page.results.map {
            row(
                "bubble.left.and.bubble.right",
                $0.title.isEmpty ? appText(.nativeSwiftChatNew) : .verbatim($0.title),
                .verbatim($0.id)
            )
        }
    }

    private func loadDirectMessageRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        if let rows = try await loadModmailMessageRows(client: client) {
            return rows
        }
        if let conversationId = routeMatch?.param("conversationId"), conversationId != "new" {
            return try await loadDirectMessageConversationRows(client: client, conversationId: conversationId)
        }
        if routeMatch?.path == "/messages/new" {
            return [
                row(
                    "square.and.pencil",
                    appText(.nativeSwiftDirectMessagesNewMessage),
                    appText(.nativeSwiftDirectMessagesStartConversation)
                )
            ]
        }
        let page: Page<DirectConversation> = try await client.send(.myMessages(limit: 50))
        return page.results.map {
            row(
                "message",
                .verbatim($0.title),
                .verbatim(($0.participantUsernames ?? []).joined(separator: ", "))
            )
        }
    }

    private func loadDirectMessageConversationRows(
        client: APIClient,
        conversationId: String
    ) async throws -> [NativeRouteDestinationRow] {
        async let messages: Page<DirectMessage> = client.send(
            .myMessageConversationMessages(conversationId: conversationId, limit: 25)
        )
        async let participants: Page<ConversationParticipant> = client.send(
            .myMessageConversationParticipants(conversationId: conversationId)
        )
        let loadedMessages = try await messages
        let loadedParticipants = try await participants
        return [
            row(
                "message",
                appText(.nativeSwiftDirectMessagesConversation, parameters: ["id": conversationId]),
                countText(loadedParticipants.results.count, item: "participant")
            )
        ] + loadedMessages.results.map {
            row(
                "bubble.left",
                $0.senderUsername.map(UiVerbatimText.verbatim)
                    ?? appText(.nativeSwiftCommunitiesMessage),
                .verbatim($0.bodyText)
            )
        }
    }

    private func loadModmailMessageRows(client: APIClient) async throws -> [NativeRouteDestinationRow]? {
        guard routeMatch?.path.hasPrefix("/messages/modmail/") == true,
              let communitySlug = routeMatch?.param("communitySlug"),
              let threadId = routeMatch?.param("threadId")
        else {
            return nil
        }
        let response: CommunityModmailMessagesResponse = try await client.send(
            .communityModmailMessages(idOrSlug: communitySlug, conversationId: threadId, limit: 25)
        )
        return [
            row(
                "tray.full",
                appText(
                    .nativeSwiftDirectMessagesModmailForCommunity,
                    parameters: ["community": communitySlug]
                ),
                countText(response.results.count, item: "message")
            )
        ] + response.results.map {
            row(
                "bubble.left",
                $0.senderUsername.map(UiVerbatimText.verbatim)
                    ?? appText(.nativeSwiftCommunitiesMessage),
                .verbatim($0.bodyText)
            )
        }
    }

    private var chatConversationId: String? {
        let conversationId = routeMatch?.param("conversationId")
            ?? routeMatch?.param("threadId")
            ?? routeMatch?.param("id")
        return conversationId == "new" ? nil : conversationId
    }

    private func loadChatDetailRows(client: APIClient, conversationId: String) async throws
        -> [NativeRouteDestinationRow] {
        let page: ChatMessagesResponse = try await client.send(
            .myConversationMessages(conversationId: conversationId, limit: 25)
        )
        return [
            row(
                "bubble.left.and.bubble.right",
                appText(.nativeSwiftDirectMessagesConversation, parameters: ["id": conversationId]),
                countText(page.results.count, item: "message")
            )
        ] + page.results.map {
            row(
                "bubble.left",
                .verbatim($0.content.role),
                .verbatim($0.content.displayText.isEmpty ? $0.id : $0.content.displayText)
            )
        }
    }

    func loadNotificationRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        try await loadNotificationPage(client: client, after: nil).rows.map(\.row)
    }

    func loadNotificationPage(client: APIClient, after: String?) async throws -> NativeForwardPage {
        let response: NativeNotificationsResponse = try await client.send(.notifications(after: after, limit: 25))
        let rows = response.results.compactMap { result -> NativeForwardRow? in
            guard let notification = response.notifications[result.id] else { return nil }
            return forwardRow(
                id: result.id,
                icon: "bell",
                title: .verbatim(notification.title),
                detail: .verbatim(notification.body)
            )
        }
        return NativeForwardPage(rows: rows, pageInfo: response.pageInfo)
    }

}
