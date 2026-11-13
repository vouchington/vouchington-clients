import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif

public struct SessionSignOutRequest: @unchecked Sendable {
    let request: URLRequest
    public let persistenceError: Error?
}

public extension APIClient {
    /// Captures the old credentials and clears them locally before the logout request can suspend.
    func prepareSignOut(
        expectedEpoch: UInt64,
        afterClear: (@Sendable () -> Void)? = nil
    ) async throws -> SessionSignOutRequest {
        let raw = try buildRequest(.logout)
        let signed = await applySigningIfNeeded(raw, method: "POST")
        return try cookieCoordinator.captureSignOut(
            signed,
            origin: baseURL,
            expectedEpoch: expectedEpoch,
            persist: checkedClearPersistence, afterClear: afterClear
        )
    }

    /// The captured request is independent of subsequent cookie changes. Its response cannot change the jar.
    func finishSignOut(_ prepared: SessionSignOutRequest) async throws {
        let (data, response) = try await session.data(for: prepared.request)
        try validate(response: response, data: data)
    }
}

extension SessionCookieCoordinator {
    func captureSignOut(
        _ request: URLRequest, origin: URL, expectedEpoch: UInt64, persist: (() throws -> Void)?,
        afterClear: (() -> Void)?
    ) throws -> SessionSignOutRequest {
        try lock.withLock {
            guard epoch == expectedEpoch else { throw CancellationError() }
            let (captured, _) = try prepare(request, origin: origin)
            beginSignOut()
            defer { endSignOut() }
            var failure: Error?
            do { try clear(persist: persist) } catch { failure = error }
            afterClear?()
            return SessionSignOutRequest(request: captured, persistenceError: failure)
        }
    }
}
