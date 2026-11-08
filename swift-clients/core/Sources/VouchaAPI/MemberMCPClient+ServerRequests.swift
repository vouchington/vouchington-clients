import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif

extension MemberMCPClient {
    func sendServerReply(_ data: Data, token: String, session: String?, version: String?) async throws {
        try Task.checkCancellation()
        try retryCooldown.check()
        var request = URLRequest(url: endpoint)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue("application/json, text/event-stream", forHTTPHeaderField: "Accept")
        request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        if let session { request.setValue(session, forHTTPHeaderField: "Mcp-Session-Id") }
        if let version { request.setValue(version, forHTTPHeaderField: "MCP-Protocol-Version") }
        request.httpBody = data
        let connection = URLSession(
            configuration: configuration, delegate: MCPNoRedirectDelegate.shared, delegateQueue: nil
        )
        defer { connection.invalidateAndCancel() }
        let (_, response) = try await connection.data(for: request)
        guard let http = response as? HTTPURLResponse else { throw Failure.invalidResponse }
        try throwIfTransportRejected(http, session: session)
        guard http.statusCode == 202 else { throw Failure.httpStatus(http.statusCode) }
    }

    func throwIfTransportRejected(_ http: HTTPURLResponse, session: String?) throws {
        if http.statusCode == 401 { throw Failure.unauthorized }
        if http.statusCode == 404, let session {
            if sessionId == session {
                initialized = false
                protocolVersion = nil
                sessionId = nil
            }
            throw Failure.sessionExpired
        }
        if http.statusCode == 429 { throw rateLimitFailure(http) }
    }
}
