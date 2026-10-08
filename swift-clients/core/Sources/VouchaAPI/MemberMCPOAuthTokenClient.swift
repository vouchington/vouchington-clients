import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif

public actor MemberMCPOAuthTokenClient {
    public enum Failure: Error, Sendable { case invalidGrant, invalidResponse, httpStatus(Int) }

    private struct ProtocolError: Decodable { let error: String }

    private let endpoint: URL
    private let revocationEndpoint: URL
    private let clientId: URL
    private let resource: URL
    private let session: URLSession

    public init(
        metadata: MemberMCPOAuthMetadata,
        clientId: URL,
        resource: URL,
        protocolClasses: [AnyClass]? = nil
    ) throws {
        guard metadata.tokenEndpoint.scheme == "https", metadata.revocationEndpoint.scheme == "https",
              clientId.scheme == "https",
              resource.scheme == "https" else { throw Failure.invalidResponse }
        endpoint = metadata.tokenEndpoint
        revocationEndpoint = metadata.revocationEndpoint
        self.clientId = clientId
        self.resource = resource
        let config = URLSessionConfiguration.ephemeral
        config.httpCookieStorage = nil
        config.httpShouldSetCookies = false
        config.httpCookieAcceptPolicy = .never
        if let protocolClasses { config.protocolClasses = protocolClasses }
        session = URLSession(configuration: config)
    }

    public func redeem(code: String, verifier: String, redirectURI: URL) async throws -> MemberMCPOAuthTokens {
        try await exchange([
            URLQueryItem(name: "grant_type", value: "authorization_code"),
            URLQueryItem(name: "code", value: code),
            URLQueryItem(name: "code_verifier", value: verifier),
            URLQueryItem(name: "redirect_uri", value: redirectURI.absoluteString)
        ])
    }

    public func refresh(_ refreshToken: String) async throws -> MemberMCPOAuthTokens {
        try await exchange([
            URLQueryItem(name: "grant_type", value: "refresh_token"),
            URLQueryItem(name: "refresh_token", value: refreshToken)
        ])
    }

    public func revoke(_ refreshToken: String) async throws {
        var form = URLComponents()
        form.queryItems = [
            URLQueryItem(name: "client_id", value: clientId.absoluteString),
            URLQueryItem(name: "token", value: refreshToken)
        ]
        guard let data = form.percentEncodedQuery?.replacingOccurrences(of: "+", with: "%2B")
            .data(using: .utf8) else { throw Failure.invalidResponse }
        var request = URLRequest(url: revocationEndpoint)
        request.httpMethod = "POST"
        request.setValue("application/x-www-form-urlencoded", forHTTPHeaderField: "Content-Type")
        request.httpBody = data
        let (_, response) = try await session.data(for: request)
        guard let http = response as? HTTPURLResponse else { throw Failure.invalidResponse }
        guard http.statusCode == 200 else { throw Failure.httpStatus(http.statusCode) }
    }

    private func exchange(_ fields: [URLQueryItem]) async throws -> MemberMCPOAuthTokens {
        var form = URLComponents()
        form.queryItems = fields + [
            URLQueryItem(name: "client_id", value: clientId.absoluteString),
            URLQueryItem(name: "resource", value: resource.absoluteString)
        ]
        guard let data = form.percentEncodedQuery?.replacingOccurrences(of: "+", with: "%2B")
            .data(using: .utf8) else { throw Failure.invalidResponse }
        var request = URLRequest(url: endpoint)
        request.httpMethod = "POST"
        request.setValue("application/x-www-form-urlencoded", forHTTPHeaderField: "Content-Type")
        request.httpBody = data
        let (body, response) = try await session.data(for: request)
        guard let http = response as? HTTPURLResponse else { throw Failure.invalidResponse }
        if http.statusCode == 400,
           let error = try? JSONDecoder().decode(ProtocolError.self, from: body),
           error.error == "invalid_grant" { throw Failure.invalidGrant }
        guard http.statusCode == 200 else { throw Failure.httpStatus(http.statusCode) }
        let tokens = try JSONDecoder().decode(MemberMCPOAuthTokens.self, from: body)
        guard tokens.tokenType == "Bearer", !tokens.accessToken.isEmpty,
              !tokens.refreshToken.isEmpty, tokens.expiresIn > 0
        else { throw Failure.invalidResponse }
        return tokens
    }
}
