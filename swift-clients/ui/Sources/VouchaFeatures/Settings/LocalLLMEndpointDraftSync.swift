import VouchaCore

struct LocalLLMEndpointDraftSync {
    let draft: LocalLLMEndpointDraft
    let syncedDraft: LocalLLMEndpointDraft
    let didClearSecretState: Bool

    static func resolve(
        oldProfile: LocalLLMEndpointProfile,
        newProfile: LocalLLMEndpointProfile,
        currentDraft: LocalLLMEndpointDraft,
        syncedDraft: LocalLLMEndpointDraft
    ) -> LocalLLMEndpointDraftSync {
        let sameSecret = oldProfile.id == newProfile.id && oldProfile.origin == newProfile.origin
        guard sameSecret else {
            let incomingDraft = LocalLLMEndpointDraft(profile: newProfile)
            return LocalLLMEndpointDraftSync(
                draft: incomingDraft,
                syncedDraft: incomingDraft,
                didClearSecretState: true
            )
        }
        guard currentDraft.matchesExceptAPIKey(syncedDraft) else {
            return LocalLLMEndpointDraftSync(draft: currentDraft, syncedDraft: syncedDraft, didClearSecretState: false)
        }
        var nextDraft = LocalLLMEndpointDraft(profile: newProfile)
        nextDraft.apiKey = currentDraft.apiKey
        return LocalLLMEndpointDraftSync(draft: nextDraft, syncedDraft: nextDraft, didClearSecretState: false)
    }
}
