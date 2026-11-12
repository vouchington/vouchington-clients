import Foundation
import VouchaCore
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif

extension APIClient {
    var checkedPersistence: (() throws -> Void)? {
        if let persistCookiesChecked { return persistCookiesChecked }
        return persistCookies
    }

    var checkedClearPersistence: (() throws -> Void)? {
        clearCookiesChecked ?? checkedPersistence
    }

    func responseCookies(_ response: URLResponse) -> [HTTPCookie] {
        guard let response = response as? HTTPURLResponse, let url = response.url,
              url.scheme == baseURL.scheme, url.host == baseURL.host, url.port == baseURL.port else { return [] }
        let headers = response.allHeaderFields.reduce(into: [String: String]()) { result, entry in
            result[String(describing: entry.key)] = String(describing: entry.value)
        }
        return HTTPCookie.cookies(withResponseHeaderFields: headers, for: url)
    }

    func acceptCookies(
        _ response: URLResponse,
        stamp: SessionCookieCoordinator.Stamp,
        establishesSession: Bool = false
    ) throws {
        let successful = (response as? HTTPURLResponse).map { (200 ... 299).contains($0.statusCode) } == true
        try cookieCoordinator.commit(
            responseCookies(response),
            stamp: stamp,
            persist: checkedPersistence,
            clearPersistence: checkedClearPersistence,
            establishingSession: establishesSession && successful
        )
    }

    func executeBuffered(_ endpoint: Endpoint) async throws -> (Data, URLResponse) {
        let raw = try buildRequest(endpoint)
        if endpoint.path == "/api/v1/session", endpoint.method == .PATCH {
            let stamp = cookieCoordinator.stamp
            let explicit = raw.httpBody.flatMap { try? JSONDecoder().decode([String: String].self, from: $0) } ?? [:]
            let (result, requiresIdentity) = try await cookieCoordinator.rotateExplicit(
                explicit, origin: baseURL, expectedEpoch: stamp.epoch
            ) { stamp in
                try await self.performSessionRotation(stamp: stamp, endpoint: endpoint)
            }
            if requiresIdentity { try requireAuthenticatedSession(epoch: stamp.epoch) }
            return (result.data, result.response)
        }
        return try await mutationData(for: raw, endpoint: endpoint)
    }

    func executeBuffered(
        _ raw: URLRequest, endpoint: Endpoint, mayRecover: Bool, expectedEpoch: UInt64? = nil
    ) async throws -> (Data, URLResponse) {
        let (prepared, stamp) = try cookieCoordinator.prepare(
            raw,
            origin: baseURL,
            establishingSession: endpoint.establishesSession
        )
        if let expectedEpoch, expectedEpoch != stamp.epoch { throw CancellationError() }
        let request = await applySigningIfNeeded(prepared, method: endpoint.method.rawValue)
        try cookieCoordinator.check(stamp)
        let (data, response): (Data, URLResponse)
        do {
            (data, response) = try await session.data(for: request)
        } catch let error as URLError { throw VouchaError.network(error) }
        try cookieCoordinator.check(stamp)
        if mayRecover, canReplaySessionRequest(raw, path: endpoint.path), try await recoverSessionIfNeeded(
            response: response,
            data: data,
            stamp: stamp,
            path: endpoint.path
        ) {
            return try await executeBuffered(raw, endpoint: endpoint, mayRecover: false, expectedEpoch: stamp.epoch)
        }
        if try clearDefinitiveSessionFailure(response: response, data: data, stamp: stamp, afterRecovery: !mayRecover) {
            return (data, response)
        }
        try acceptCookies(response, stamp: stamp, establishesSession: endpoint.establishesSession)
        return (data, response)
    }

    func canReplaySessionRequest(_ request: URLRequest, path: String) -> Bool {
        guard !path.hasPrefix("/api/v1/auth/"), path != "/api/v1/session" else { return false }
        return request.value(forHTTPHeaderField: RequestSignatureHeader.challengeId) == nil
            && request.value(forHTTPHeaderField: RequestSignatureHeader.assertion) == nil
    }

    func recoverSessionIfNeeded(
        response: URLResponse, data: Data, stamp: SessionCookieCoordinator.Stamp, path: String
    ) async throws -> Bool {
        guard sessionBootstrapEnabled, path != "/api/v1/session",
              let http = response as? HTTPURLResponse, http.statusCode == 401,
              let failure = APIErrorPayload.decode(from: data, logger: logger),
              failure.code == "AUTH_REQUIRED" else { return false }
        _ = try await cookieCoordinator.rotate(since: stamp) { stamp in
            try await self.performRecoveryRotation(stamp)
        }
        try requireAuthenticatedSession(epoch: stamp.epoch)
        return true
    }

    func requireAuthenticatedSession(epoch: UInt64) throws {
        let stamp = cookieCoordinator.stamp
        guard stamp.epoch == epoch else { throw CancellationError() }
        guard cookieCoordinator.hasConfirmedIdentity else {
            try cookieCoordinator.clear(
                ifCurrent: stamp,
                preservingAuthentication: true,
                persist: checkedClearPersistence
            )
            throw VouchaError.unauthorized
        }
    }

    func clearDefinitiveSessionFailure(
        response: URLResponse, data: Data, stamp: SessionCookieCoordinator.Stamp, afterRecovery: Bool
    ) throws -> Bool {
        guard let http = response as? HTTPURLResponse, http.statusCode == 401,
              let code = APIErrorPayload.decode(from: data, logger: logger)?.code,
              Self.terminalSessionFailures.contains(code),
              code != "AUTH_REQUIRED" || afterRecovery else { return false }
        try cookieCoordinator.clear(
            ifCurrent: stamp, preservingAuthentication: stamp.authenticationOrder == nil,
            persist: checkedClearPersistence
        )
        return true
    }

    private func performRecoveryRotation(_ stamp: SessionCookieCoordinator
        .Stamp) async throws -> SessionRotationResult {
        try await performSessionRotation(stamp: stamp)
    }
}
