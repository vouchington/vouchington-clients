import Foundation
import VouchaAPI
import VouchaLocalization
import VouchaModels

private struct CommunityAgentPromptTestResult: Decodable {
    let flagged: Bool
}

extension CommunityDetailViewModel {
    func enableCommunityAiAgent(agentSlug: String) async {
        let agentSlug = agentSlug.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !agentSlug.isEmpty else { return }
        await perform(
            .enableCommunityAiAgent(idOrSlug: slug, agentSlug: agentSlug),
            success: UiMessage(.nativeSwiftCommunityStatusEnabledAiAgent)
        )
    }

    func disableCommunityAiAgent(agentSlug: String) async {
        let agentSlug = agentSlug.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !agentSlug.isEmpty else { return }
        await perform(
            .disableCommunityAiAgent(idOrSlug: slug, agentSlug: agentSlug),
            success: UiMessage(.nativeSwiftCommunityStatusDisabledAiAgent)
        )
    }

    func createCommunityAgentPrompt(
        prompt: String,
        modelName: String? = nil,
        modelProvider: String? = nil
    ) async {
        let prompt = prompt.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !prompt.isEmpty else { return }
        await perform(
            .createCommunityAgentPrompt(
                idOrSlug: slug,
                prompt: prompt,
                modelName: modelName?.trimmedOrNil,
                modelProvider: modelProvider?.trimmedOrNil
            ),
            success: UiMessage(.nativeSwiftCommunityStatusCreatedAgentPrompt)
        )
    }

    func deleteCommunityAgentPrompt(promptId: String) async {
        let promptId = promptId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !promptId.isEmpty else { return }
        await perform(
            .deleteCommunityAgentPrompt(idOrSlug: slug, promptId: promptId),
            success: UiMessage(.nativeSwiftCommunityStatusDeletedAgentPrompt)
        )
    }

    func updateCommunityAgentPrompt(promptId: String, prompt: String) async {
        let promptId = promptId.trimmingCharacters(in: .whitespacesAndNewlines)
        let prompt = prompt.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !promptId.isEmpty, !prompt.isEmpty else { return }
        await perform(
            .updateCommunityAgentPrompt(idOrSlug: slug, promptId: promptId, prompt: prompt),
            success: UiMessage(.nativeSwiftCommunityStatusUpdatedAgentPrompt)
        )
    }

    func allocateCommunityAgentPrompt(promptId: String) async {
        let promptId = promptId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !promptId.isEmpty else { return }
        await perform(
            .allocateCommunityAgentPromptSlot(idOrSlug: slug, promptId: promptId),
            success: UiMessage(.nativeSwiftCommunityStatusAllocatedAgentPrompt)
        )
    }

    func deallocateCommunityAgentPrompt(promptId: String) async {
        let promptId = promptId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !promptId.isEmpty else { return }
        await perform(
            .deallocateCommunityAgentPromptSlot(idOrSlug: slug, promptId: promptId),
            success: UiMessage(.nativeSwiftCommunityStatusDeallocatedAgentPrompt)
        )
    }

    @discardableResult
    func testCommunityAgentPrompt(
        promptId: String,
        text: String,
        saveForTraining: Bool? = nil,
        expectedFlagged: Bool? = nil
    ) async -> Bool? {
        let promptId = promptId.trimmingCharacters(in: .whitespacesAndNewlines)
        let text = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !promptId.isEmpty, !text.isEmpty, let client else { return nil }
        state = .loading
        do {
            let result: CommunityAgentPromptTestResult = try await client.send(
                .testCommunityAgentPrompt(
                    idOrSlug: slug,
                    promptId: promptId,
                    text: text,
                    saveForTraining: saveForTraining,
                    expectedFlagged: expectedFlagged
                )
            )
            statusMessage = UiMessage(.nativeSwiftCommunityStatusTestedAgentPrompt)
            await load()
            return result.flagged
        } catch {
            state = .error(UiMessage(.nativeSwiftCommunityStatusActionFailed))
            return nil
        }
    }

    func recordCommunityAutomodFeedback(
        sourceKey: String,
        outcome: CommunityAutomodFeedbackOutcome,
        action: CommunityAutomodFeedbackAction,
        reasonCode: String? = nil,
        note: String? = nil
    ) async {
        let sourceKey = sourceKey.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !sourceKey.isEmpty else { return }
        await perform(
            .recordCommunityAutomodFeedback(
                idOrSlug: slug,
                sourceKey: sourceKey,
                outcome: outcome,
                action: action,
                reasonCode: reasonCode?.trimmedOrNil,
                note: note?.trimmedOrNil
            ),
            success: UiMessage(.nativeSwiftCommunityStatusRecordedAutomodFeedback)
        )
    }

    func loadCommunityAutomodRecentActions(limit: Int = 10) async {
        await loadInitialCommunityAutomodActions(limit: limit)
    }

    func simulateCommunityAutomod(
        promptId: String,
        prompt: String? = nil,
        timeWindowHours: Int? = nil,
        limit: Int? = nil
    ) async {
        let promptId = promptId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !promptId.isEmpty else { return }
        await perform(
            .simulateCommunityAutomod(
                idOrSlug: slug,
                body: CommunityAutomodSimulationBody(
                    promptId: promptId,
                    prompt: prompt?.trimmedOrNil,
                    timeWindowHours: timeWindowHours,
                    limit: limit
                )
            ),
            success: UiMessage(.nativeSwiftCommunityStatusSimulatedAutomod)
        )
    }
}
