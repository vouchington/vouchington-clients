import VouchaLocalization
import VouchaModels

extension NativeRouteSurfaceViewModel {
    func agentConversationHeaderDetail(_ conversation: AgentConversation) -> UiVerbatimText {
        appText(
            .nativeSwiftRouteSurfaceDateValue,
            dateParameters: [
                "date": UiMessageDateParameter(
                    conversation.createdAt,
                    dateStyle: .abbreviated,
                    timeStyle: .shortened
                )
            ]
        )
    }

    func agentDirectoryDetail(_ agent: AgentSummary) -> UiVerbatimText {
        .joined([
            rawText(agent.agentType),
            appText(
                agent.activatedAt != nil && agent.deactivatedAt == nil
                    ? .nativeSwiftRouteSurfaceAgentStatusActive
                    : .nativeSwiftRouteSurfaceAgentStatusInactive
            ),
            appText(
                .nativeSwiftRouteSurfaceDateValue,
                dateParameters: [
                    "date": UiMessageDateParameter(
                        agent.createdAt,
                        dateStyle: .abbreviated,
                        timeStyle: .omitted
                    )
                ]
            )
        ])
    }

    var routeAgentConversationFilter: AgentConversationFilter? {
        let queryPrecedence: [AgentConversationFilterKind] = [
            .userId, .username, .postId, .postSlug, .rssFeedItemId
        ]
        for kind in queryPrecedence {
            if let value = routeMatch?.queryValue(kind.rawValue), let filter = AgentConversationFilter(
                kind: kind,
                value: value
            ) {
                return filter
            }
        }
        return nil
    }

    func hydrateAgentConversationFilterFromRoute() {
        let routeIdentity = [routeMatch?.path ?? "", routeQuery ?? ""].joined(separator: "|")
        guard agentConversationFilterRouteIdentity != routeIdentity else { return }
        agentConversationFilterRouteIdentity = routeIdentity
        agentConversationFilter = routeAgentConversationFilter
    }
}
