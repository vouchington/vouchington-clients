import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif

public struct MemberMCPOAuthMetadata: Sendable {
    public let issuerIdentifier: String
    public let authorizationEndpoint: URL
    public let tokenEndpoint: URL
    public let revocationEndpoint: URL
}

private struct MCPResourceMetadataDocument: Decodable {
    let resource: URL
    let authorizationServers: [String]

    enum CodingKeys: String, CodingKey {
        case resource
        case authorizationServers = "authorization_servers"
    }
}

private struct MCPAuthorizationServerDocument: Decodable {
    let issuer: String
    let authorizationEndpoint: URL
    let tokenEndpoint: URL
    let revocationEndpoint: URL

    enum CodingKeys: String, CodingKey {
        case issuer
        case authorizationEndpoint = "authorization_endpoint"
        case tokenEndpoint = "token_endpoint"
        case revocationEndpoint = "revocation_endpoint"
    }
}

/// Discovers the member resource and its authorization server without sharing session cookies.
public actor MemberMCPOAuthDiscovery {
    public enum Failure: Error, Sendable { case invalidMetadata, httpStatus(Int) }

    private let siteOrigin: URL
    private let session: URLSession

    public init(siteOrigin: URL, protocolClasses: [AnyClass]? = nil) throws {
        let origin = URLComponents(url: siteOrigin, resolvingAgainstBaseURL: false)
        guard origin?.scheme == "https", origin?.host != nil,
              origin?.user == nil, origin?.password == nil,
              origin?.query == nil, origin?.fragment == nil,
              origin?.path.isEmpty == true || origin?.path == "/"
        else { throw Failure.invalidMetadata }
        self.siteOrigin = siteOrigin
        let config = URLSessionConfiguration.ephemeral
        config.httpCookieStorage = nil
        config.httpShouldSetCookies = false
        config.httpCookieAcceptPolicy = .never
        if let protocolClasses { config.protocolClasses = protocolClasses }
        session = URLSession(configuration: config, delegate: MCPNoRedirectDelegate.shared, delegateQueue: nil)
    }

    public func discover() async throws -> MemberMCPOAuthMetadata {
        let resource = siteOrigin.appendingPathComponent("api/v1/mcp")
        let url = siteOrigin.appendingPathComponent(".well-known/oauth-protected-resource/api/v1/mcp")
        let protected: MCPResourceMetadataDocument = try await read(url)
        guard protected.resource == resource, protected.authorizationServers.count == 1,
              let issuerIdentifier = protected.authorizationServers.first,
              let issuer = URLComponents(string: issuerIdentifier),
              issuer.scheme == "https", issuer.host != nil,
              issuer.user == nil, issuer.password == nil,
              issuer.query == nil, issuer.fragment == nil
        else { throw Failure.invalidMetadata }
        var metadataParts = issuer
        metadataParts.percentEncodedPath = "/.well-known/oauth-authorization-server" +
            (issuer.percentEncodedPath == "/" ? "" : issuer.percentEncodedPath)
        guard let metadataURL = metadataParts.url else { throw Failure.invalidMetadata }
        let server: MCPAuthorizationServerDocument = try await read(metadataURL)
        guard server.issuer == issuerIdentifier,
              server.authorizationEndpoint.scheme == "https",
              server.tokenEndpoint.scheme == "https",
              server.revocationEndpoint.scheme == "https"
        else { throw Failure.invalidMetadata }
        return MemberMCPOAuthMetadata(
            issuerIdentifier: issuerIdentifier, authorizationEndpoint: server.authorizationEndpoint,
            tokenEndpoint: server.tokenEndpoint, revocationEndpoint: server.revocationEndpoint
        )
    }

    private func read<Value: Decodable>(_ url: URL) async throws -> Value {
        var request = URLRequest(url: url)
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        let (data, response) = try await session.data(for: request)
        guard let http = response as? HTTPURLResponse else { throw Failure.invalidMetadata }
        guard http.statusCode == 200 else { throw Failure.httpStatus(http.statusCode) }
        return try JSONDecoder().decode(Value.self, from: data)
    }
}
