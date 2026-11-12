import Foundation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

@MainActor
extension NativeTopicManagementViewModel {
    /// The producer queues merges (202). Keep the operation and show "queued" until a status read
    /// reports `completed`; never claim the merge is done on acceptance.
    func mergeTopic() async {
        guard let client, let topicIdentifier, mergeOperation == nil, !isSubmittingMerge else { return }
        let destination = destinationIdOrSlug.trimmed
        guard !destination.isEmpty else { return }
        let generation = mergeGeneration
        let accountId = mergeAccountId
        isSubmittingMerge = true
        defer { if generation == mergeGeneration { isSubmittingMerge = false } }
        state = .loading
        do {
            let response: TopicMergeEnvelope = try await client.send(.mergeTopicAliases(
                sourceTopicId: topicIdentifier,
                destinationIdOrSlug: destination
            ))
            guard generation == mergeGeneration, accountId == mergeAccountId else { return }
            mergeOperation = response.topicMerge
            mergeMessage = Self.mergeMessage(forStatus: response.topicMerge.status)
            state = .loaded
        } catch let error as VouchaError {
            guard generation == mergeGeneration, accountId == mergeAccountId else { return }
            state = .error(error)
        } catch {
            guard generation == mergeGeneration, accountId == mergeAccountId else { return }
            state = .error(.api(statusCode: 0, preconditionCode: nil))
        }
    }

    var canCheckMergeStatus: Bool {
        guard let status = mergeOperation?.status else { return false }
        return status != "completed" && status != "failed"
    }

    /// One status read per call; there is no background polling.
    func checkMergeStatus() async {
        guard let client, let operation = mergeOperation, !isCheckingMerge else { return }
        let generation = mergeGeneration
        let accountId = mergeAccountId
        isCheckingMerge = true
        defer { if generation == mergeGeneration { isCheckingMerge = false } }
        do {
            let response: TopicMergeEnvelope = try await client.send(.topicMergeStatus(
                sourceTopicId: operation.sourceTopicId,
                operationId: operation.id
            ))
            guard generation == mergeGeneration, accountId == mergeAccountId,
                  mergeOperation?.id == operation.id,
                  mergeOperation?.sourceTopicId == operation.sourceTopicId,
                  response.topicMerge.id == operation.id,
                  response.topicMerge.sourceTopicId == operation.sourceTopicId else { return }
            mergeOperation = response.topicMerge
            mergeMessage = Self.mergeMessage(forStatus: response.topicMerge.status)
            if response.topicMerge.status == "completed" {
                state = .loaded
            }
        } catch {
            guard generation == mergeGeneration, accountId == mergeAccountId,
                  mergeOperation?.id == operation.id,
                  mergeOperation?.sourceTopicId == operation.sourceTopicId else { return }
            mergeMessage = .settingsTopicsMergeStatusFailed
        }
    }

    func updateMergeAccount(_ accountId: String?) {
        guard accountId != mergeAccountId else { return }
        mergeAccountId = accountId
        mergeGeneration = UUID()
        mergeOperation = nil
        mergeMessage = nil
        isCheckingMerge = false
        isSubmittingMerge = false
        destinationIdOrSlug = ""
        state = .idle
    }

    static func mergeMessage(forStatus status: String) -> UiMessageKey {
        switch status {
        case "completed": .settingsTopicsMergeCompleted
        case "failed": .settingsTopicsMergeFailed
        default: .settingsTopicsMergeQueued
        }
    }
}
