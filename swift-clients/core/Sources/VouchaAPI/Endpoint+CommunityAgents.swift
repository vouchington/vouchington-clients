import Foundation

private struct CommunityAgentPromptCreateBody: Encodable {
    let prompt: String
    let modelName: String?
    let modelProvider: String?
}

private struct CommunityAgentPromptUpdateBody: Encodable {
    let prompt: String?
}

private struct CommunityAgentPromptTestRunBody: Encodable {
    let text: String
    let saveForTraining: Bool?
    let expectedFlagged: Bool?
}

public extension Endpoint {
    static func communityAiAgents(idOrSlug: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/communities/\(pathSegment(idOrSlug))/ai-agents")
    }

    static func enableCommunityAiAgent(idOrSlug: String, agentSlug: String) -> Endpoint {
        Endpoint(.PUT, path: "/api/v1/communities/\(pathSegment(idOrSlug))/ai-agents/\(pathSegment(agentSlug))")
    }

    static func disableCommunityAiAgent(idOrSlug: String, agentSlug: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/communities/\(pathSegment(idOrSlug))/ai-agents/\(pathSegment(agentSlug))")
    }

    static func communityAgentPrompts(idOrSlug: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/communities/\(pathSegment(idOrSlug))/agent-prompts")
    }

    static func communityAgentPrompt(idOrSlug: String, promptId: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/communities/\(pathSegment(idOrSlug))/agent-prompts/\(pathSegment(promptId))")
    }

    static func createCommunityAgentPrompt(
        idOrSlug: String,
        prompt: String,
        modelName: String? = nil,
        modelProvider: String? = nil
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/agent-prompts",
            body: CommunityAgentPromptCreateBody(prompt: prompt, modelName: modelName, modelProvider: modelProvider)
        )
    }

    static func updateCommunityAgentPrompt(idOrSlug: String, promptId: String, prompt: String? = nil) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/agent-prompts/\(pathSegment(promptId))",
            body: CommunityAgentPromptUpdateBody(prompt: prompt)
        )
    }

    static func deleteCommunityAgentPrompt(idOrSlug: String, promptId: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/communities/\(pathSegment(idOrSlug))/agent-prompts/\(pathSegment(promptId))")
    }

    static func allocateCommunityAgentPromptSlot(idOrSlug: String, promptId: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/agent-prompts/\(pathSegment(promptId))/allocations"
        )
    }

    static func deallocateCommunityAgentPromptSlot(idOrSlug: String, promptId: String) -> Endpoint {
        Endpoint(
            .DELETE,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/agent-prompts/\(pathSegment(promptId))/allocations"
        )
    }

    static func communityAgentPromptHistory(
        idOrSlug: String,
        promptId: String? = nil,
        before: String? = nil
    ) -> Endpoint {
        var items: [URLQueryItem] = []
        if let promptId {
            items.append(.init(name: "promptId", value: promptId))
        }
        if let before {
            items.append(.init(name: "before", value: before))
        }
        return Endpoint(
            .GET,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/agent-prompts/history",
            queryItems: items
        )
    }

    static func testCommunityAgentPrompt(
        idOrSlug: String,
        promptId: String,
        text: String,
        saveForTraining: Bool? = nil,
        expectedFlagged: Bool? = nil
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/agent-prompts/\(pathSegment(promptId))/test-runs",
            body: CommunityAgentPromptTestRunBody(
                text: text,
                saveForTraining: saveForTraining,
                expectedFlagged: expectedFlagged
            )
        )
    }
}
