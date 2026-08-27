import Foundation
import VouchaLocalization
import VouchaModels

extension StaffSupportViewModel {
    func assignToMe() async {
        guard let client, let selection = activeThreadSelection, let administratorId else { return }
        await mutate(selection) {
            let response: SupportThreadResponse = try await client.send(
                .assignStaffSupportThread(threadId: selection.id, administratorId: administratorId)
            )
            guard isCurrent(selection) else { return }
            replaceThread(response.thread)
        }
    }

    func setResolved(_ resolved: Bool) async {
        guard let client, let selection = activeThreadSelection else { return }
        await mutate(selection) {
            let response: SupportThreadResponse = try await client.send(
                .setStaffSupportThreadResolved(threadId: selection.id, resolved: resolved)
            )
            guard isCurrent(selection) else { return }
            replaceThread(response.thread)
        }
    }

    func saveOutboundReply() async {
        guard let client, let selection = activeThreadSelection, canMutateSelectedThreadDrafts else { return }
        let body = replyText.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !body.isEmpty else { return }
        await mutate(selection) {
            let response: SupportMessageResponse = try await client.send(
                .createStaffSupportMessage(threadId: selection.id, bodyText: body)
            )
            guard isCurrent(selection) else { return }
            replaceMessage(response.message)
            replyText = ""
        }
    }

    func generateDraft() async {
        guard let client,
              let selection = activeThreadSelection,
              canMutateSelectedThreadDrafts,
              canGenerateDraft,
              !isWaitingForDraft,
              !isMutating
        else { return }
        isWaitingForDraft = true
        isMutating = true
        let errorGeneration = beginErrorOperation()
        let knownMessageIds = Set(messages.map(\.id))
        defer {
            isWaitingForDraft = false
            isMutating = false
        }
        do {
            let _: SupportDraftQueuedResponse = try await client.send(.queueStaffSupportDraft(threadId: selection.id))
            guard isCurrent(selection) else { return }
            if try await awaitQueuedDraft(selection, knownMessageIds: knownMessageIds) {
                return
            }
            guard isCurrent(selection) else { return }
            recordError(.message(.nativeSwiftCommonTryAgain), for: .thread(selection.id), generation: errorGeneration)
        } catch {
            await reconcileDraftQueueFailure(
                error,
                selection: selection,
                knownMessageIds: knownMessageIds,
                errorGeneration: errorGeneration
            )
        }
    }

    func saveDraft(_ message: SupportMessage) async {
        guard let client, let selection = activeThreadSelection, canMutateSelectedThreadDrafts else { return }
        await mutate(selection) {
            let response: SupportMessageResponse = try await client.send(
                .updateStaffSupportDraft(
                    threadId: selection.id,
                    messageId: message.id,
                    bodyText: draftText(for: message)
                )
            )
            guard isCurrent(selection) else { return }
            replaceMessage(response.message)
        }
    }

    func approve(_ message: SupportMessage) async {
        guard let client,
              let selection = activeThreadSelection,
              canMutateSelectedThreadDrafts,
              !isDraftDirty(message)
        else { return }
        await mutate(selection) {
            let response: SupportMessageResponse = try await client.send(
                .approveStaffSupportMessage(threadId: selection.id, messageId: message.id)
            )
            guard isCurrent(selection) else { return }
            replaceMessage(response.message)
        }
    }

    func send(_ message: SupportMessage) async {
        guard let client, let selection = activeThreadSelection, canMutateSelectedThreadDrafts else { return }
        await mutate(selection) {
            do {
                let response: SupportMessageResponse = try await client.send(
                    .sendStaffSupportMessage(threadId: selection.id, messageId: message.id)
                )
                guard isCurrent(selection) else { return }
                replaceMessage(response.message)
            } catch {
                if isCurrent(selection) {
                    let refetch = await selectThread(selection.id, preservingComposerState: true)
                    if refetch.didLoad, let refetchSelection = refetch.selection, isCurrent(refetchSelection) {
                        recordError(
                            error,
                            for: .thread(selection.id),
                            generation: refetchSelection.errorOperationGeneration
                        )
                    }
                }
                throw error
            }
        }
    }

    private func mutate(_ selection: ThreadSelection, _ operation: () async throws -> Void) async {
        guard !isMutating else { return }
        isMutating = true
        let errorGeneration = beginErrorOperation()
        defer { isMutating = false }
        do {
            try await operation()
        } catch {
            guard isCurrent(selection) else { return }
            recordError(error, for: .thread(selection.id), generation: errorGeneration)
        }
    }
}
