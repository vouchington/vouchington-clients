import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif

public extension MemberMCPClient {
    /// Terminates a stateful MCP session before the client is released.
    func close(accessToken: String) async throws {
        guard !accessToken.isEmpty else { throw Failure.unauthorized }
        let closingSession = sessionId
        let version = protocolVersion
        initialized = false
        sessionId = nil
        protocolVersion = nil
        guard let closingSession else { return }
        var request = URLRequest(url: endpoint)
        request.httpMethod = "DELETE"
        request.setValue("Bearer \(accessToken)", forHTTPHeaderField: "Authorization")
        request.setValue(closingSession, forHTTPHeaderField: "Mcp-Session-Id")
        if let version { request.setValue(version, forHTTPHeaderField: "MCP-Protocol-Version") }
        let connection = URLSession(
            configuration: configuration, delegate: MCPNoRedirectDelegate.shared, delegateQueue: nil
        )
        defer { connection.invalidateAndCancel() }
        let (_, response) = try await connection.data(for: request)
        guard let http = response as? HTTPURLResponse else { throw Failure.invalidResponse }
        if http.statusCode == 401 { throw Failure.unauthorized }
        guard [200, 202, 204, 404, 405].contains(http.statusCode) else {
            throw Failure.httpStatus(http.statusCode)
        }
    }
}
