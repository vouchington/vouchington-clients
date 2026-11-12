import Foundation
import Observation

/// One native prompt at a time; parallel mutations fail closed rather than sharing a proof.
@MainActor
@Observable
public final class MutationChallengeBroker {
    public private(set) var pendingID: UUID?
    private var continuation: CheckedContinuation<String?, Never>?
    private var presentationOwner: UUID?

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
        presentationOwner = nil
        pendingID = nil
        waiting?.resume(returning: token)
    }

    /// All windows share this broker; only one presenter may own the current prompt.
    public func claimPresentation(id: UUID, owner: UUID) -> Bool {
        guard pendingID == id, presentationOwner == nil || presentationOwner == owner else { return false }
        presentationOwner = owner
        return true
    }

    public func finishPresentation(id: UUID, owner: UUID, token: String?) {
        guard presentationOwner == owner else { return }
        finish(id: id, token: token)
    }

    public func cancel() {
        if let pendingID { finish(id: pendingID, token: nil) }
    }
}
