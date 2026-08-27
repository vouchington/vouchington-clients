import VouchaAPI
import VouchaLocalization
import VouchaModels

extension NativeRouteSurfaceViewModel {
    var agentDirectoryResults: [AgentSummary] {
        get { agentDirectoryPagination.items }
        set { agentDirectoryPagination.replaceItems(newValue) }
    }

    var agentDirectoryPageInfo: Page<AgentSummary>.PageInfo? {
        get {
            guard agentDirectoryPagination.hasLoadedPage else { return nil }
            return .init(hasNextPage: agentDirectoryPagination.hasMore, endCursor: agentDirectoryPagination.endCursor)
        }
        set {
            guard let newValue else {
                agentDirectoryPagination.reset(items: agentDirectoryResults)
                return
            }
            agentDirectoryPagination.restoreContinuation(endCursor: newValue.endCursor, hasMore: newValue.hasNextPage)
        }
    }

    public var isLoadingMoreAgents: Bool {
        agentDirectoryPagination.isLoading && agentDirectoryPagination.hasLoadedPage
    }

    var agentConversationListResults: [AgentConversationSummary] {
        get { agentConversationListPagination.items }
        set { agentConversationListPagination.replaceItems(newValue) }
    }

    var agentConversationListPageInfo: Page<AgentConversationSummary>.PageInfo? {
        get {
            guard agentConversationListPagination.hasLoadedPage else { return nil }
            return .init(
                hasNextPage: agentConversationListPagination.hasMore,
                endCursor: agentConversationListPagination.endCursor
            )
        }
        set {
            guard let newValue else {
                agentConversationListPagination.reset(items: agentConversationListResults)
                return
            }
            agentConversationListPagination.restoreContinuation(
                endCursor: newValue.endCursor,
                hasMore: newValue.hasNextPage
            )
        }
    }

    public var isLoadingMoreAgentConversations: Bool {
        agentConversationListPagination.isLoading && agentConversationListPagination.hasLoadedPage
    }

    func agentDirectoryRows() -> [NativeRouteDestinationRow] {
        agentDirectoryResults.map { agent in
            .init(
                id: "agent-directory:\(agent.id)",
                icon: "bubble.left.and.bubble.right",
                title: rawText(agentDirectoryDisplayName(agent)),
                detail: agentDirectoryDetail(agent),
                targetPath: NativeAgentPath.detail(agent.id).value
            )
        }
    }

    func agentConversationListRows() -> [NativeRouteDestinationRow] {
        agentConversationListResults.map { conversation in
            .init(
                id: "agent-conversation-list:\(conversation.id)",
                icon: "message",
                title: agentConversationTitle(conversation.title),
                detail: agentConversationListDetail(conversation),
                targetPath: NativeAgentPath.conversation(
                    agentIdOrSlug: routeMatch?.param("idOrSlug") ?? "",
                    conversationId: conversation.id
                ).value
            )
        }
    }

    func agentConversationRows(fallbackId: String, agentSystemUserId: String?) -> [NativeRouteDestinationRow] {
        guard let conversation = agentConversation else { return [] }
        return [
            NativeRouteDestinationRow(
                id: "agent-conversation:\(conversation.id):header",
                icon: "bubble.left.and.bubble.right",
                title: agentConversationTitle(conversation.title, fallbackId: fallbackId),
                detail: agentConversationHeaderDetail(conversation)
            )
        ] + agentConversationMessages.map { message in
            NativeRouteDestinationRow(
                id: "agent-conversation:\(conversation.id):message:\(message.id)",
                icon: "bubble.left",
                title: agentMessageRole(message.createdById, agentSystemUserId: agentSystemUserId),
                detail: .joined([
                    agentMessageBody(message.content),
                    appText(
                        .nativeSwiftRouteSurfaceDateValue,
                        dateParameters: [
                            "date": UiMessageDateParameter(
                                message.createdAt,
                                dateStyle: .abbreviated,
                                timeStyle: .shortened
                            )
                        ]
                    )
                ])
            )
        }
    }

    func agentDisplayName(_ detail: AgentDetailResponse) -> String {
        [detail.user?.displayAccount?.name, detail.user?.name, detail.user?.username, detail.agent.systemUserId]
            .compactMap { $0?.trimmingCharacters(in: .whitespacesAndNewlines) }
            .first(where: { !$0.isEmpty }) ?? detail.agent.systemUserId
    }

    func agentConversationCreatorName(_ conversation: AgentConversationSummary) -> UiVerbatimText {
        guard let createdById = conversation.createdById else {
            return appText(.nativeSwiftPresentationValuesDeleted)
        }

        return [
            agentConversationListUsers[createdById]?.displayAccount?.name,
            agentConversationListUsers[createdById]?.username
        ]
        .compactMap { $0?.trimmingCharacters(in: .whitespacesAndNewlines) }
        .first(where: { !$0.isEmpty })
        .map(rawText) ?? rawText(String(createdById.prefix(8)))
    }

    func agentDirectoryDisplayName(_ agent: AgentSummary) -> String {
        [
            agentDirectoryUsers[agent.systemUserId]?.displayAccount?.name,
            agentDirectoryUsers[agent.systemUserId]?.username
        ]
        .compactMap { $0?.trimmingCharacters(in: .whitespacesAndNewlines) }
        .first(where: { !$0.isEmpty }) ?? String(agent.systemUserId.prefix(8))
    }

    func agentConversationListDetail(_ conversation: AgentConversationSummary) -> UiVerbatimText {
        .joined([
            agentConversationCreatorName(conversation),
            appText(
                .nativeSwiftRouteSurfaceDateValue,
                dateParameters: [
                    "date": UiMessageDateParameter(
                        conversation.createdAt,
                        dateStyle: .abbreviated,
                        timeStyle: .omitted
                    )
                ]
            )
        ])
    }

    func agentConversationTitle(_ title: String) -> UiVerbatimText {
        title.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            ? appText(.nativeSwiftRouteSurfaceAgentConversation)
            : rawText(title)
    }

    func agentConversationTitle(_ title: String, fallbackId: String) -> UiVerbatimText {
        title.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            ? appText(
                .nativeSwiftRouteSurfaceAgentConversationWithId,
                textParameters: ["id": .protocolValue(fallbackId)]
            )
            : rawText(title)
    }

    func agentMessageBody(_ content: AgentConversationMessageContent?) -> UiVerbatimText {
        if let value = content?.content, !value.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            return .userContent(value)
        }
        if let error = content?.error, !error.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            return .protocolValue(error)
        }
        return appText(.nativeSwiftRouteSurfaceNoResults)
    }

    func isCurrentAgentPage(revision: Int, pageRevision: Int) -> Bool {
        revision == agentDirectoryLoadRevision
            && pageRevision == agentDirectoryPageRequestRevision
            && routeMatch?.path == "/agents"
    }

    func isCurrentAgentConversationPage(_ agentId: String, revision: Int, pageRevision: Int) -> Bool {
        revision == agentConversationListLoadRevision
            && pageRevision == agentConversationListPageRequestRevision
            && routeMatch?.param("idOrSlug") == agentId
            && routeMatch?.param("conversationId") == nil
    }

}
