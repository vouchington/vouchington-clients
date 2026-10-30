import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
import VouchaModels

extension MemberMCPClient {
    func ensureInitialized(token: String) async throws {
        if closed { throw Failure.closed }
        if initialized { return }
        if let initialization { return try await initialization.value }
        let task = Task { try await initialize(token: token) }
        initialization = task
        defer { initialization = nil }
        try await task.value
    }

    private func initialize(token: String) async throws {
        protocolVersion = nil
        sessionId = nil
        do {
            let params: DecodedJSONValue = .object([
                "protocolVersion": .string("2025-06-18"),
                "capabilities": .object([:]),
                "clientInfo": .object(["name": .string("voucha-native"), "version": .string("1.0.0")])
            ])
            let result: MCPInitializationResult = try await sendRPC(
                method: "initialize", params: params, token: token
            )
            guard result.protocolVersion == "2025-06-18" else { throw Failure.invalidResponse }
            protocolVersion = result.protocolVersion
            var request = URLRequest(url: endpoint)
            request.httpMethod = "POST"
            request.setValue("application/json", forHTTPHeaderField: "Content-Type")
            request.setValue("application/json, text/event-stream", forHTTPHeaderField: "Accept")
            request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
            request.setValue(result.protocolVersion, forHTTPHeaderField: "MCP-Protocol-Version")
            if let sessionId { request.setValue(sessionId, forHTTPHeaderField: "Mcp-Session-Id") }
            request.httpBody = try JSONEncoder().encode(DecodedJSONValue.object([
                "jsonrpc": .string("2.0"), "method": .string("notifications/initialized")
            ]))
            let session = URLSession(
                configuration: configuration,
                delegate: MCPNoRedirectDelegate.shared,
                delegateQueue: nil
            )
            defer { session.invalidateAndCancel() }
            try retryCooldown.check()
            let (_, response) = try await session.data(for: request)
            guard let http = response as? HTTPURLResponse else { throw Failure.invalidResponse }
            try throwIfTransportRejected(http, session: nil)
            guard http.statusCode == 202 else { throw Failure.httpStatus(http.statusCode) }
            initialized = true
        } catch {
            initialized = false
            protocolVersion = nil
            sessionId = nil
            throw error
        }
    }
}
