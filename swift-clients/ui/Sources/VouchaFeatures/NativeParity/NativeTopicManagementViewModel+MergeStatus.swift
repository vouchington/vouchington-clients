import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

@MainActor
extension NativeTopicManagementViewModel {
    /// The producer queues merges (202). Keep the operation and show "queued" until a status read
    /// reports `completed`; never claim the merge is done on acceptance.
    func mergeTopic() async {
        guard let client, let topicIdentifier, mergeOperation == nil else { return }
        let destination = destinationIdOrSlug.trimmed
        guard !destination.isEmpty else { return }
        state = .loading
        do {
            let response: TopicMergeEnvelope = try await client.send(.mergeTopicAliases(
                sourceTopicId: topicIdentifier,
                destinationIdOrSlug: destination
            ))
            mergeOperation = response.topicMerge
            mergeMessage = Self.mergeMessage(forStatus: response.topicMerge.status)
            state = .loaded
        } catch let error as VouchaError {
            state = .error(error)
        } catch {
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
        isCheckingMerge = true
        defer { isCheckingMerge = false }
        do {
            let response: TopicMergeEnvelope = try await client.send(.topicMergeStatus(
                sourceTopicId: operation.sourceTopicId,
                operationId: operation.id
            ))
            mergeOperation = response.topicMerge
            mergeMessage = Self.mergeMessage(forStatus: response.topicMerge.status)
            if response.topicMerge.status == "completed" {
                state = .loaded
            }
        } catch {
            mergeMessage = .settingsTopicsMergeStatusFailed
        }
    }

    static func mergeMessage(forStatus status: String) -> UiMessageKey {
        switch status {
        case "completed": .settingsTopicsMergeCompleted
        case "failed": .settingsTopicsMergeFailed
        default: .settingsTopicsMergeQueued
        }
    }
}
