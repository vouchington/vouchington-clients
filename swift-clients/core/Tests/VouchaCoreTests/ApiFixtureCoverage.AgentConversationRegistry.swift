import VouchaModels

let nativeAgentConversationCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.agents.default") {
        try assertFixtureCoversDTO($0, as: AgentListResponse.self)
    },
    RegisteredFixture(id: "native.agents.page-2") {
        try assertFixtureCoversDTO($0, as: AgentListResponse.self)
    },
    RegisteredFixture(id: "native.agents.detail.default") {
        try assertFixtureCoversDTO($0, as: AgentDetailResponse.self)
    },
    RegisteredFixture(id: "native.agents.conversations.default") {
        try assertFixtureCoversDTO($0, as: AgentConversationListResponse.self)
    },
    RegisteredFixture(id: "native.agents.conversations.page-2") {
        try assertFixtureCoversDTO($0, as: AgentConversationListResponse.self)
    },
    RegisteredFixture(id: "native.agents.conversations.filtered-username") {
        try assertFixtureCoversDTO($0, as: AgentConversationListResponse.self)
    },
    RegisteredFixture(id: "native.agents.conversation.default") {
        try assertFixtureCoversDTO($0, as: AgentConversationDetailResponse.self)
    },
    RegisteredFixture(id: "native.agents.conversation.page-2") {
        try assertFixtureCoversDTO($0, as: AgentConversationDetailResponse.self)
    }
]
