import Foundation
import VouchaCore
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif

extension SessionCookieCoordinator {
    func awaitRotation() async throws {
        let task = lock.withLock { rotation?.1 }
        _ = try await task?.value
        try Task.checkCancellation()
    }

    func rotate(
        since observed: Stamp?, operation: @escaping @Sendable (Stamp) async throws -> SessionRotationResult
    ) async throws -> SessionRotationResult? {
        guard let task = rotationTask(since: observed, operation: operation) else { return nil }
        let result = try await task.value
        try Task.checkCancellation()
        return result
    }

    func rotateExplicit(
        _ explicit: [String: String], origin: URL, expectedEpoch: UInt64,
        operation: @escaping @Sendable (Stamp) async throws -> SessionRotationResult
    ) async throws -> (SessionRotationResult, Bool) {
        let (task, requiresIdentity) = try lock.withLock {
            guard epoch == expectedEpoch else { throw CancellationError() }
            let current = try tokens(for: origin, stamp: stamp)
            guard !explicit.contains(where: { key, value in current[key].map { $0 != value } ?? false }) else {
                throw VouchaError.unauthorized
            }
            return (sharedRotationTask(operation), !current.isEmpty || !explicit.isEmpty)
        }
        let result = try await task.value
        try Task.checkCancellation()
        return (result, requiresIdentity)
    }

    func rotationTask(
        since observed: Stamp?, operation: @escaping @Sendable (Stamp) async throws -> SessionRotationResult
    ) -> Task<SessionRotationResult, Error>? {
        lock.withLock {
            if let observed, observed.epoch != epoch { return Task { throw CancellationError() } }
            if let rotation { return rotation.1 }
            if let observed, observed.revision != revision { return nil }
            return sharedRotationTask(operation)
        }
    }

    private func sharedRotationTask(
        _ operation: @escaping @Sendable (Stamp) async throws -> SessionRotationResult
    ) -> Task<SessionRotationResult, Error> {
        if signingOut { return Task { throw CancellationError() } }
        if let rotation { return rotation.1 }
        let id = UUID()
        let started = stamp
        let task = Task {
            defer { self.finishRotation(id) }
            return try await operation(started)
        }
        rotation = (id, task)
        return task
    }

    private func finishRotation(_ id: UUID) {
        lock.withLock { if rotation?.0 == id { rotation = nil } }
    }
}
