import Foundation
import VouchaCore
import VouchaLocalization
import VouchaModels

extension StaffSupportViewModel {
    func reconcileDraftQueueFailure(
        _ error: Error,
        selection: ThreadSelection,
        knownMessageIds: Set<String>,
        errorGeneration: Int
    ) async {
        guard isCurrent(selection) else { return }
        if error is CancellationError || error.isVouchaCancellation {
            return
        }
        guard isAmbiguousDraftQueueFailure(error) else {
            recordError(error, for: .thread(selection.id), generation: errorGeneration)
            return
        }
        do {
            if try await awaitQueuedDraft(selection, knownMessageIds: knownMessageIds) {
                return
            }
            recordError(.message(.nativeSwiftCommonTryAgain), for: .thread(selection.id), generation: errorGeneration)
        } catch {
            guard isCurrent(selection) else { return }
            if error is CancellationError {
                return
            }
            recordError(error, for: .thread(selection.id), generation: errorGeneration)
        }
    }

    private func isAmbiguousDraftQueueFailure(_ error: Error) -> Bool {
        if let error = error as? VouchaError {
            switch error {
            case .unauthorized, .forbidden, .notFound, .api, .apiMessage, .sessionExpired:
                return false
            case .network, .decodingFailed, .unexpected:
                return true
            case .cancelled:
                return false
            }
        }
        return true
    }

    func awaitQueuedDraft(_ selection: ThreadSelection, knownMessageIds: Set<String>) async throws -> Bool {
        let pollingTask = Task { @MainActor [weak self] in
            guard let self else { return false }
            return try await reconcileQueuedDraft(selection, knownMessageIds: knownMessageIds)
        }
        draftPollingTask = pollingTask
        defer {
            if isCurrent(selection) {
                draftPollingTask = nil
            }
        }
        do {
            return try await pollingTask.value
        } catch {
            if pollingTask.isCancelled {
                throw CancellationError()
            }
            throw error
        }
    }

    private func reconcileQueuedDraft(_ selection: ThreadSelection, knownMessageIds: Set<String>) async throws -> Bool {
        guard let client else { return false }
        for attempt in 0 ..< 20 {
            try Task.checkCancellation()
            let response: SupportMessageListResponse = try await client.send(
                .staffSupportMessages(threadId: selection.id)
            )
            guard isCurrent(selection) else { return false }
            if response.results.contains(where: { $0.draftedAt != nil && !knownMessageIds.contains($0.id) }) {
                messages = merge(existing: messages, incoming: response.results)
                return true
            }
            if attempt < 19 {
                try await Task.sleep(for: .seconds(3))
            }
        }
        return false
    }
}

private extension Error {
    var isVouchaCancellation: Bool {
        guard let error = self as? VouchaError else { return false }
        guard case .cancelled = error else { return false }
        return true
    }
}
