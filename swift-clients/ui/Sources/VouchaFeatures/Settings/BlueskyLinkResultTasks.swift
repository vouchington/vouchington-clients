import Foundation

/// Holds the latest Bluesky result task so a caller can wait for the reload the observer started.
final class BlueskyLinkResultTasks: @unchecked Sendable {
    private let lock = NSLock()
    private var task: Task<Void, Never>?

    func set(_ task: Task<Void, Never>) {
        lock.withLock { self.task = task }
    }

    func wait() async {
        let task = lock.withLock { self.task }
        await task?.value
    }
}
