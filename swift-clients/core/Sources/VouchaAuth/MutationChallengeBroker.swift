import Foundation
import Observation

/// One native prompt at a time; parallel mutations fail closed rather than sharing a proof.
@MainActor
@Observable
public final class MutationChallengeBroker {
    public private(set) var pendingID: UUID?
    private var continuation: CheckedContinuation<String?, Never>?

    public init() {}

    public func request() async -> String? {
        guard pendingID == nil, !Task.isCancelled else { return nil }
        let id = UUID()
        return await withTaskCancellationHandler {
            await withCheckedContinuation { continuation in
                self.continuation = continuation
                pendingID = id
                if Task.isCancelled { finish(id: id, token: nil) }
            }
        } onCancel: {
            Task { @MainActor in self.finish(id: id, token: nil) }
        }
    }

    public func finish(id: UUID, token: String?) {
        guard pendingID == id else { return }
        let waiting = continuation
        continuation = nil
        pendingID = nil
        waiting?.resume(returning: token)
    }

    public func cancel() {
        if let pendingID { finish(id: pendingID, token: nil) }
    }
}
