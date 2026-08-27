import Foundation
import Observation
import VouchaCore

@Observable
@MainActor
final class LocalLLMEndpointEditorInteractionState {
    var draft: LocalLLMEndpointDraft
    var syncedDraft: LocalLLMEndpointDraft
    var apiKeyWasEdited = false
    var loadedAPIKey = ""

    init(profile: LocalLLMEndpointProfile) {
        let initialDraft = LocalLLMEndpointDraft(profile: profile)
        draft = initialDraft
        syncedDraft = initialDraft
    }

    func applyLoadedAPIKey(_ key: String, isCancelled: Bool) {
        guard !isCancelled else { return }
        guard !apiKeyWasEdited else {
            loadedAPIKey = key
            apiKeyWasEdited = draft.apiKey != key
            return
        }
        loadedAPIKey = key
        draft.apiKey = key
        apiKeyWasEdited = false
    }

    func noteAPIKeyChanged(to newValue: String) {
        apiKeyWasEdited = newValue != loadedAPIKey
    }

    func applyProfileChange(from oldProfile: LocalLLMEndpointProfile, to newProfile: LocalLLMEndpointProfile) {
        let resolution = LocalLLMEndpointDraftSync.resolve(
            oldProfile: oldProfile,
            newProfile: newProfile,
            currentDraft: draft,
            syncedDraft: syncedDraft
        )
        draft = resolution.draft
        syncedDraft = resolution.syncedDraft
        if resolution.didClearSecretState {
            loadedAPIKey = ""
            apiKeyWasEdited = false
        }
    }

    func markSubmitted(_ submittedDraft: LocalLLMEndpointDraft) {
        syncedDraft = submittedDraft
    }

    func applySuccessfulSave(submittedDraft: LocalLLMEndpointDraft) {
        guard draft.apiKey == submittedDraft.apiKey else { return }
        let normalized = submittedDraft.apiKey.trimmingCharacters(in: .whitespacesAndNewlines)
        draft.apiKey = normalized
        loadedAPIKey = normalized
        apiKeyWasEdited = false
    }
}
