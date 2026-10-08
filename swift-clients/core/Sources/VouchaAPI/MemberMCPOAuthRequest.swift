import Crypto
import Foundation

public enum MemberMCPApp: String, Sendable {
    case ios, macos, windows
}

/// The state and verifier must be retained only until the matching system-browser callback.
public struct MemberMCPOAuthRequest: Sendable {
    public enum Failure: Error, Sendable {
        case invalidMetadata
        case invalidCallback
        case stateMismatch
        case issuerMismatch
        case authorizationDenied(String)
    }

    public let state: String
    public let verifier: String
    public let clientId: URL
    public let redirectURI: URL
    public let resource: URL
    public let issuerIdentifier: String
    public let authorizationURL: URL

    public init(
        siteOrigin: URL,
        app: MemberMCPApp,
        issuerIdentifier: String,
        authorizationEndpoint: URL,
        redirectURI: URL,
        scope: String = "mcp.user:read"
    ) throws {
        let origin = URLComponents(url: siteOrigin, resolvingAgainstBaseURL: false)
        guard origin?.scheme == "https", origin?.host != nil,
              origin?.user == nil, origin?.password == nil,
              origin?.query == nil, origin?.fragment == nil,
              origin?.path.isEmpty == true || origin?.path == "/",
              let issuer = URLComponents(string: issuerIdentifier), issuer.scheme == "https",
              issuer.host != nil, issuer.user == nil, issuer.password == nil,
              issuer.query == nil, issuer.fragment == nil,
              authorizationEndpoint.scheme == "https",
              !scope.isEmpty
        else { throw Failure.invalidMetadata }
        let clientId = siteOrigin.appendingPathComponent("api/v1/oauth/native-clients/\(app.rawValue)")
        let resource = siteOrigin.appendingPathComponent("api/v1/mcp")
        let expectedRedirect = siteOrigin.appendingPathComponent("oauth/native/\(app.rawValue)/callback")
        switch app {
        case .ios, .macos:
            guard redirectURI == expectedRedirect else { throw Failure.invalidMetadata }
        case .windows:
            guard redirectURI.scheme == "http",
                  redirectURI.host == "127.0.0.1" || redirectURI.host == "::1" || redirectURI.host == "[::1]",
                  redirectURI.path == "/oauth/native/windows/callback",
                  let port = redirectURI.port, port > 0,
                  redirectURI.user == nil, redirectURI.password == nil,
                  redirectURI.query == nil, redirectURI.fragment == nil
            else { throw Failure.invalidMetadata }
        }
        self.clientId = clientId
        self.redirectURI = redirectURI
        self.resource = resource
        self.issuerIdentifier = issuerIdentifier
        let random = SystemRandomNumberGenerator()
        var generator = random
        state = Self.randomToken(using: &generator)
        verifier = Self.randomToken(using: &generator)
        let digest = SHA256.hash(data: Data(verifier.utf8))
        let challenge = Data(digest).base64URLEncodedString()
        authorizationURL = try Self.authorizationURL(endpoint: authorizationEndpoint, parameters: [
            URLQueryItem(name: "response_type", value: "code"),
            URLQueryItem(name: "client_id", value: clientId.absoluteString),
            URLQueryItem(name: "redirect_uri", value: redirectURI.absoluteString),
            URLQueryItem(name: "scope", value: scope),
            URLQueryItem(name: "resource", value: resource.absoluteString),
            URLQueryItem(name: "state", value: state),
            URLQueryItem(name: "code_challenge", value: challenge),
            URLQueryItem(name: "code_challenge_method", value: "S256")
        ])
    }

    private static func authorizationURL(endpoint: URL, parameters: [URLQueryItem]) throws -> URL {
        guard var parts = URLComponents(url: endpoint, resolvingAgainstBaseURL: false),
              !(parts.queryItems ?? []).contains(where: { item in
                  parameters.contains(where: { $0.name == item.name })
              })
        else { throw Failure.invalidMetadata }
        parts.queryItems = (parts.queryItems ?? []) + parameters
        guard let url = parts.url else { throw Failure.invalidMetadata }
        return url
    }

    public func code(from callback: URL) throws -> String {
        guard callback.scheme == redirectURI.scheme, callback.host == redirectURI.host,
              callback.port == redirectURI.port, callback.path == redirectURI.path,
              callback.user == nil, callback.password == nil, callback.fragment == nil,
              let parts = URLComponents(url: callback, resolvingAgainstBaseURL: false),
              let items = parts.queryItems
        else { throw Failure.invalidCallback }
        let states = items.filter { $0.name == "state" }
        let issuers = items.filter { $0.name == "iss" }
        let codes = items.filter { $0.name == "code" }
        let errors = items.filter { $0.name == "error" }
        guard states.count == 1, issuers.count == 1,
              codes.count + errors.count == 1,
              let stateValue = states.first?.value,
              let issuerValue = issuers.first?.value
        else { throw Failure.invalidCallback }
        guard stateValue == state else { throw Failure.stateMismatch }
        guard issuerValue == issuerIdentifier else { throw Failure.issuerMismatch }
        if let error = errors.first?.value {
            throw Failure.authorizationDenied(error)
        }
        guard let code = codes.first?.value, !code.isEmpty
        else { throw Failure.invalidCallback }
        return code
    }

    private static func randomToken(using generator: inout SystemRandomNumberGenerator) -> String {
        let bytes = (0 ..< 32).map { _ in UInt8.random(in: .min ... .max, using: &generator) }
        return Data(bytes).base64URLEncodedString()
    }
}

private extension Data {
    func base64URLEncodedString() -> String {
        base64EncodedString().replacingOccurrences(of: "+", with: "-")
            .replacingOccurrences(of: "/", with: "_").replacingOccurrences(of: "=", with: "")
    }
}
