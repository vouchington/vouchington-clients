import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif

extension APIClient {
    func ensureSessionBootstrap() async throws {
        guard sessionBootstrapEnabled, !hasDeviceCookie else { return }
        if let sessionBootstrapTask {
            try await sessionBootstrapTask.value
            return
        }
        let task = Task { try await performSessionBootstrap() }
        sessionBootstrapTask = task
        defer { sessionBootstrapTask = nil }
        try await task.value
    }

    private func performSessionBootstrap() async throws {
        let endpoint = Endpoint(.PATCH, path: "/api/v1/session", body: [String: String]())
        let request = try buildRequest(endpoint)
        let (data, response) = try await session.data(for: request)
        try validate(response: response, data: data)
        try persistSessionTokens(from: data)
    }

    private var hasDeviceCookie: Bool {
        cookieStorage.cookies(for: baseURL)?.contains { $0.name == "dt" && !$0.value.isEmpty } == true
    }

    func persistSessionTokensIfPresent(from data: Data) throws {
        guard let response = try? decoder.decode(SessionBootstrapResponse.self, from: data) else { return }
        persistSessionTokens(response.session)
    }

    private func persistSessionTokens(from data: Data) throws {
        let response = try decoder.decode(SessionBootstrapResponse.self, from: data)
        persistSessionTokens(response.session)
    }

    private func persistSessionTokens(_ session: SessionBootstrapPayload) {
        let cookies = [
            ("dt", session.deviceToken, Date().addingTimeInterval(TimeInterval(session.deviceExpiration))),
            ("st", session.sessionToken, Date().addingTimeInterval(TimeInterval(session.sessionExpiration)))
        ]
        for (name, value, expiration) in cookies {
            var properties: [HTTPCookiePropertyKey: Any] = [
                .name: name,
                .value: value,
                .path: "/",
                .originURL: baseURL,
                .expires: expiration
            ]
            if session.secure {
                properties[.secure] = "TRUE"
            }
            if let cookie = HTTPCookie(properties: properties) {
                cookieStorage.setCookie(cookie)
            }
        }
    }

    static func makeSession(
        configuration: URLSessionConfiguration,
        baseURL: URL,
        pinningPolicy: PublicKeyPinningPolicy
    ) -> URLSession {
        #if canImport(Security)
            guard pinningPolicy.requiresPinning(for: baseURL) else {
                return URLSession(configuration: configuration)
            }

            let delegate = PublicKeyPinningURLSessionDelegate(policy: pinningPolicy)
            return URLSession(configuration: configuration, delegate: delegate, delegateQueue: nil)
        #else
            return URLSession(configuration: configuration)
        #endif
    }
}

private struct SessionBootstrapResponse: Decodable {
    let session: SessionBootstrapPayload
}

private struct SessionBootstrapPayload: Decodable {
    let deviceToken: String
    let sessionToken: String
    let deviceExpiration: Int
    let sessionExpiration: Int
    let secure: Bool

    private enum CodingKeys: String, CodingKey {
        case deviceToken = "dt"
        case sessionToken = "st"
        case deviceExpiration = "dte"
        case sessionExpiration = "ste"
        case secure
    }
}
