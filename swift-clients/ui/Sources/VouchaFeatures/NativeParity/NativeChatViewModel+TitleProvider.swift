import Foundation
import VouchaAPI
import VouchaModels

extension NativeChatViewModel {
    func selectTitleProvider(_ selection: NativeChatTitleProviderKind) {
        titleProviderSelection = selection
        let revision = titleProviderPersistenceState.advance()
        let persistenceState = titleProviderPersistenceState
        let resolver = titleProviderResolver
        let precedingTask = titleProviderPersistenceTask
        titleProviderPersistenceTask = Task { [weak self] in
            await precedingTask?.value
            guard persistenceState.isCurrent(revision) else { return }
            guard await resolver.persistSelection(selection) else {
                guard persistenceState.isCurrent(revision), let self else { return }
                titleProviderSelection = resolver.defaultSelection()
                return
            }
        }
    }

    func generateTitleIfNeeded(conversationId: String) async {
        guard shouldGenerateTitle(conversationId: conversationId) else { return }
        let canUseLocalTitleProvider = selectedConversationId == conversationId
        let titleMessages = canUseLocalTitleProvider ? messages : []

        isGeneratingTitle = true
        defer { isGeneratingTitle = false }

        let provider = titleProviderResolver.provider(for: titleProviderSelection)
        do {
            if canUseLocalTitleProvider, let title = try await provider.generateTitle(from: titleMessages) {
                guard shouldGenerateTitle(conversationId: conversationId) else { return }
                let previousConversation = conversations.first { $0.id == conversationId }
                guard let generatedTitle = applyGeneratedConversationTitle(title, conversationId: conversationId) else {
                    return
                }
                await persistGeneratedConversationTitle(
                    generatedTitle,
                    conversationId: conversationId,
                    previousConversation: previousConversation
                )
                return
            }
        } catch {
            logger.error("Unable to generate chat conversation title locally: \(error.localizedDescription)")
        }

    }

    private func shouldGenerateTitle(conversationId: String) -> Bool {
        guard selectedConversationId == conversationId || createdConversationIds.contains(conversationId) else {
            return false
        }
        if selectedConversationId == conversationId {
            return conversationTitleDraft.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
        }
        let title = conversations.first { $0.id == conversationId }?.title ?? ""
        return title.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    private func persistGeneratedConversationTitle(
        _ title: String,
        conversationId: String,
        previousConversation: ChatConversation?
    ) async {
        guard let client else { return }
        do {
            guard isGeneratedTitleStillCurrent(title, conversationId: conversationId) else { return }
            let response: ChatConversationResponse = try await client.send(
                .renameConversation(conversationId: conversationId, title: title)
            )
            guard isGeneratedTitleStillCurrent(title, conversationId: conversationId) else {
                await repairGeneratedTitleRace(conversationId: conversationId)
                return
            }
            updateConversation(id: conversationId) { $0 = response.conversation }
            if selectedConversationId == conversationId {
                conversationTitleDraft = response.conversation.title
            }
        } catch {
            if let previousConversation,
               isGeneratedTitleStillCurrent(title, conversationId: conversationId) {
                updateConversation(id: conversationId) { $0 = previousConversation }
                if selectedConversationId == conversationId {
                    conversationTitleDraft = previousConversation.title
                }
            }
            logger.error("Unable to persist chat conversation title: \(error.localizedDescription)")
        }
    }

    private func repairGeneratedTitleRace(conversationId: String) async {
        guard let client else { return }
        let title = currentConversationTitle(conversationId: conversationId)
        guard title.isEmpty == false else { return }
        do {
            _ = try await client.send(.renameConversation(
                conversationId: conversationId,
                title: title
            )) as ChatConversationResponse
        } catch {
            logger
                .error(
                    "Generated title race repair failed: \(error.localizedDescription)"
                )
        }
    }

    private func isGeneratedTitleStillCurrent(_ title: String, conversationId: String) -> Bool {
        let trimmed = title.trimmingCharacters(in: .whitespacesAndNewlines)
        let conversationTitle = conversations.first { $0.id == conversationId }?.title
            .trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if selectedConversationId == conversationId {
            return conversationTitleDraft.trimmingCharacters(in: .whitespacesAndNewlines) == trimmed &&
                conversationTitle == trimmed
        }
        return conversationTitle == trimmed
    }

    private func currentConversationTitle(conversationId: String) -> String {
        if selectedConversationId == conversationId {
            let draft = conversationTitleDraft.trimmingCharacters(in: .whitespacesAndNewlines)
            if draft.isEmpty == false {
                return draft
            }
        }
        return conversations.first { $0.id == conversationId }?.title
            .trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
    }

    private func applyGeneratedConversationTitle(_ title: String, conversationId: String) -> String? {
        let trimmed = title.trimmingCharacters(in: .whitespacesAndNewlines)
        guard trimmed.isEmpty == false else { return nil }
        updateConversation(id: conversationId) { $0.title = trimmed }
        if selectedConversationId == conversationId {
            conversationTitleDraft = trimmed
        }
        return trimmed
    }
}

final class NativeChatTitleProviderPersistenceState: @unchecked Sendable {
    private let lock = NSLock()
    private var latestRevision = 0

    func advance() -> Int {
        lock.lock()
        defer { lock.unlock() }
        latestRevision += 1
        return latestRevision
    }

    func isCurrent(_ revision: Int) -> Bool {
        lock.lock()
        defer { lock.unlock() }
        return latestRevision == revision
    }
}
