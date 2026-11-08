import VouchaModels

extension CommunityDetailViewModel {
    private func ownsAgentPrompt(_ id: String) -> Bool {
        guard let currentUserId,
              let prompt = agentPromptWorkspace.pagination.items.first(where: { $0.id == id }),
              let creator = prompt.createdById else { return false }
        return creator == currentUserId
    }

    func canOwnAgentPrompt(_ id: String) -> Bool {
        ownsAgentPrompt(id)
    }

    func canUpdateAgentPrompt(_ id: String) -> Bool {
        ownsAgentPrompt(id) && (isAdministrator || communityDetail?.membership != nil)
    }

    func canDeleteAgentPrompt(_ id: String) -> Bool {
        guard agentPromptWorkspace.pagination.items.contains(where: { $0.id == id }) else { return false }
        return isAdministrator || communityDetail?.membership?.role == .owner
            || (ownsAgentPrompt(id) && communityDetail?.membership != nil)
    }
}
