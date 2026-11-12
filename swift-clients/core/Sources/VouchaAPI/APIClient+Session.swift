import Foundation
import VouchaCore
import VouchaModels
#if !canImport(Darwin)
    import FoundationNetworking
#endif

extension APIClient {
    func ensureSessionBootstrap() async throws {
        guard sessionBootstrapEnabled else { return }
        let observed = cookieCoordinator.stamp
        if !hasDeviceCookie {
            _ = try await rotateSession(since: observed)
        } else {
            try await cookieCoordinator.awaitRotation()
        }
    }

    /// Rotates first-party credentials before resolving identity on lifecycle refreshes.
    public func refreshSession() async throws {
        let stamp = cookieCoordinator.stamp
        _ = try await rotateSession(since: stamp)
        try requireAuthenticatedSession(epoch: stamp.epoch)
    }

    public nonisolated func invalidateSessionRequests() {
        cookieCoordinator.invalidate()
    }

    public nonisolated var sessionEpoch: UInt64 {
        cookieCoordinator.stamp.epoch
    }

    func rotateSession(
        since observed: SessionCookieCoordinator.Stamp,
        endpoint: Endpoint? = nil
    ) async throws -> SessionRotationResult? {
        try await cookieCoordinator.rotate(since: observed) { stamp in
            try await self.performSessionRotation(stamp: stamp, endpoint: endpoint)
        }
    }

    func performSessionRotation(
        stamp: SessionCookieCoordinator.Stamp,
        endpoint: Endpoint? = nil
    ) async throws -> SessionRotationResult {
        let endpoint = try endpoint ?? Endpoint(
            .PATCH,
            path: "/api/v1/session",
            body: cookieCoordinator.tokens(for: baseURL, stamp: stamp)
        )
        let raw = try buildRequest(endpoint)
        let request = await applySigningIfNeeded(raw, method: endpoint.method.rawValue)
        let (prepared, _) = try cookieCoordinator.prepare(request, origin: baseURL)
        try cookieCoordinator.check(stamp)
        let (data, response) = try await session.data(for: prepared)
        try cookieCoordinator.check(stamp)
        if let http = response as? HTTPURLResponse, http.statusCode == 401,
           let code = APIErrorPayload.decode(from: data, logger: logger)?.code,
           Self.terminalSessionFailures.contains(code) {
            try cookieCoordinator.clear(
                ifCurrent: stamp,
                preservingAuthentication: true,
                persist: checkedClearPersistence
            )
        }
        try validate(response: response, data: data)
        try commitSessionTokens(from: data, response: response, stamp: stamp)
        return SessionRotationResult(data: data, response: response)
    }

    private var hasDeviceCookie: Bool {
        cookieStorage.cookies(for: baseURL)?.contains { $0.name == "dt" && !$0.value.isEmpty } == true
    }

    func commitSessionTokens(
        from data: Data, response: URLResponse, stamp: SessionCookieCoordinator.Stamp
    ) throws {
        let payload: NativeRefreshedSession
        do {
            payload = try decoder.decode(NativeSessionRefreshResponse.self, from: data).session
            guard !payload.deviceToken.isEmpty, !payload.sessionToken.isEmpty,
                  payload.deviceExpiration > 0, payload.sessionExpiration > 0,
                  payload.uid == nil || payload.session.uid == nil || payload.uid == payload.session.uid else {
                throw VouchaError.unauthorized
            }
        } catch {
            try cookieCoordinator.clear(
                ifCurrent: stamp,
                preservingAuthentication: true,
                persist: checkedClearPersistence
            )
            throw VouchaError.unauthorized
        }
        var cookies = responseCookies(response)
        for (name, value, lifetime) in [
            ("dt", payload.deviceToken, payload.deviceExpiration),
            ("st", payload.sessionToken, payload.sessionExpiration)
        ] {
            let existing = cookies.first { $0.name == name }
            var properties = existing?.properties ?? [
                .originURL: baseURL, .path: "/", HTTPCookiePropertyKey("HttpOnly"): "TRUE"
            ]
            properties[.name] = name
            properties[.value] = value
            properties[.expires] = Date().addingTimeInterval(TimeInterval(lifetime))
            properties.removeValue(forKey: .maximumAge)
            properties[.secure] = payload.secure ? "TRUE" : nil
            guard let cookie = HTTPCookie(properties: properties) else { throw VouchaError.unauthorized }
            cookies.removeAll { $0.name == name }
            cookies.append(cookie)
        }
        try cookieCoordinator.commit(
            cookies,
            stamp: stamp,
            persist: checkedPersistence,
            clearPersistence: checkedClearPersistence,
            rotated: true,
            authenticated: !(payload.uid ?? payload.session.uid ?? "").isEmpty
        )
    }

    static let terminalSessionFailures: Set<String> = [
        "AUTH_REQUIRED", "SESSION_RELOGIN_REQUIRED", "SESSION_REFRESH_REUSED", "SESSION_USER_AGENT_MISMATCH",
        "SESSION_JA4_MISMATCH"
    ]

    static func makeSession(
        configuration: URLSessionConfiguration,
        baseURL: URL,
        pinningPolicy: PublicKeyPinningPolicy
    ) -> URLSession {
        #if canImport(Security)
            guard pinningPolicy.requiresPinning(for: baseURL) else {
                return URLSession(
                    configuration: configuration,
                    delegate: FirstPartySessionDelegate(),
                    delegateQueue: nil
                )
            }

            let delegate = PublicKeyPinningURLSessionDelegate(policy: pinningPolicy)
            return URLSession(configuration: configuration, delegate: delegate, delegateQueue: nil)
        #else
            return URLSession(configuration: configuration, delegate: FirstPartySessionDelegate(), delegateQueue: nil)
        #endif
    }
}
