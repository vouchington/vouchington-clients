import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
import VouchaModels

/// A bearer-only transport for the member MCP resource. It never shares the app's cookie session.
public actor MemberMCPClient {
    public enum Failure: Error, Sendable {
        case invalidOrigin
        case unauthorized
        case rateLimited(retryAfterSeconds: Int?)
        case httpStatus(Int)
        case invalidResponse
        case sessionExpired
        case rpc(code: Int, message: String)
        case closed
    }

    let endpoint: URL
    let configuration: URLSessionConfiguration
    let responseTimeout: TimeInterval
    var retryCooldown: MemberMCPRetryCooldown
    private var nextRequestId = 0
    var protocolVersion: String?
    var sessionId: String?
    var initialized = false
    var closed = false
    var initialization: Task<Void, Error>?

    public init(
        siteOrigin: URL, protocolClasses: [AnyClass]? = nil, responseTimeout: TimeInterval = 120,
        retryUptime: @escaping @Sendable () -> TimeInterval = { ProcessInfo.processInfo.systemUptime },
        retryDate: @escaping @Sendable () -> Date = { Date() }
    ) throws {
        let origin = URLComponents(url: siteOrigin, resolvingAgainstBaseURL: false)
        guard origin?.scheme == "https", origin?.host != nil,
              origin?.user == nil, origin?.password == nil,
              origin?.query == nil, origin?.fragment == nil,
              origin?.path.isEmpty == true || origin?.path == "/"
        else { throw Failure.invalidOrigin }
        guard responseTimeout.isFinite, responseTimeout > 0 else { throw Failure.invalidResponse }
        endpoint = siteOrigin.appendingPathComponent("api/v1/mcp")
        self.responseTimeout = responseTimeout
        retryCooldown = MemberMCPRetryCooldown(uptime: retryUptime, date: retryDate)
        let config = URLSessionConfiguration.ephemeral
        config.timeoutIntervalForResource = responseTimeout
        config.httpCookieStorage = nil
        config.httpShouldSetCookies = false
        config.httpCookieAcceptPolicy = .never
        if let protocolClasses { config.protocolClasses = protocolClasses }
        configuration = config
    }

    public func listTools(accessToken: String, cursor: String? = nil) async throws -> DecodedJSONValue {
        let params: DecodedJSONValue = cursor.map { .object(["cursor": .string($0)]) } ?? .object([:])
        return try await send(method: "tools/list", params: params, token: accessToken)
    }

    public func callTool(
        name: String,
        arguments: DecodedJSONObject,
        accessToken: String
    ) async throws -> ToolResult {
        let params: DecodedJSONValue = .object([
            "name": .string(name), "arguments": .object(arguments)
        ])
        let result: ToolResult = try await send(method: "tools/call", params: params, token: accessToken)
        if let failure = rateLimitFailure(result) { throw failure }
        return result
    }

    private func send<Result: MCPResponseValue>(
        method: String,
        params: DecodedJSONValue,
        token: String
    ) async throws -> Result {
        try Task.checkCancellation()
        try await ensureInitialized(token: token)
        do {
            return try await sendRPC(method: method, params: params, token: token)
        } catch Failure.sessionExpired {
            try await ensureInitialized(token: token)
            return try await sendRPC(method: method, params: params, token: token)
        }
    }

    func sendRPC<Result: MCPResponseValue>(
        method: String,
        params: DecodedJSONValue,
        token: String
    ) async throws -> Result {
        if closed, method != "initialize" { throw Failure.closed }
        try Task.checkCancellation()
        try retryCooldown.check()
        nextRequestId += 1
        let id = String(nextRequestId)
        let body: DecodedJSONValue = .object([
            "jsonrpc": .string("2.0"), "id": .string(id),
            "method": .string(method), "params": params
        ])
        var request = URLRequest(url: endpoint)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue("application/json, text/event-stream", forHTTPHeaderField: "Accept")
        request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        if let protocolVersion { request.setValue(protocolVersion, forHTTPHeaderField: "MCP-Protocol-Version") }
        let requestSession = sessionId
        let requestVersion = method == "initialize" ? "2025-06-18" : protocolVersion
        if let requestSession { request.setValue(requestSession, forHTTPHeaderField: "Mcp-Session-Id") }
        request.httpBody = try body.exactJSONData()
        let transport = ResponseLinesTransport(followRedirects: false, maximumLineBytes: 1_100_000)
        let deadline = responseDeadline(for: transport)
        defer {
            deadline.cancel()
            transport.cancel()
        }
        let responseLines = try await transport.start(request: request, configuration: configuration)
        let response = responseLines.response
        guard let http = response as? HTTPURLResponse else { throw Failure.invalidResponse }
        if http.statusCode == 401 { throw Failure.unauthorized }
        if http.statusCode == 404, let requestSession {
            if sessionId == requestSession {
                initialized = false
                protocolVersion = nil
                sessionId = nil
            }
            throw Failure.sessionExpired
        }
        if http.statusCode == 429 {
            throw rateLimitFailure(http)
        }
        guard http.statusCode == 200 else { throw Failure.httpStatus(http.statusCode) }
        let responseSession = try sessionForReply(to: method, response: http, previous: requestSession)
        return try await MCPResponseReader.read(
            responseLines.lines,
            contentType: http.value(forHTTPHeaderField: "Content-Type"), id: id,
            sendServerReply: { [self] data in
                try await sendServerReply(data, token: token, session: responseSession, version: requestVersion)
            }
        )
    }

    private func responseDeadline(for transport: ResponseLinesTransport) -> Task<Void, Never> {
        Task {
            try? await Task.sleep(for: .seconds(responseTimeout))
            if !Task.isCancelled { transport.cancel() }
        }
    }

}
