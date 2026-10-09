import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
import VouchaModels

struct MCPRPCReply<Result: Decodable & Sendable>: Decodable {
    struct RPCError: Decodable {
        let code: Int
        let message: String
    }

    let jsonrpc: String
    let id: String?
    let method: String?
    let result: Result?
    let error: RPCError?
}

/// A bearer-only transport for the member MCP resource. It never shares the app's cookie session.
public actor MemberMCPClient {
    public enum Failure: Error, Sendable {
        case invalidOrigin
        case unauthorized
        case rateLimited(retryAfterSeconds: Int?)
        case httpStatus(Int)
        case invalidResponse
        case rpc(code: Int, message: String)
    }

    public struct ToolResult: Decodable, Sendable {
        public let content: [DecodedJSONValue]
        public let structuredContent: DecodedJSONValue?
        public let isError: Bool?
    }

    private let endpoint: URL
    private let configuration: URLSessionConfiguration
    private var nextRequestId = 0

    public init(siteOrigin: URL, protocolClasses: [AnyClass]? = nil) throws {
        let origin = URLComponents(url: siteOrigin, resolvingAgainstBaseURL: false)
        guard origin?.scheme == "https", origin?.host != nil,
              origin?.user == nil, origin?.password == nil,
              origin?.query == nil, origin?.fragment == nil,
              origin?.path.isEmpty == true || origin?.path == "/"
        else { throw Failure.invalidOrigin }
        endpoint = siteOrigin.appendingPathComponent("api/v1/mcp")
        let config = URLSessionConfiguration.ephemeral
        config.httpCookieStorage = nil
        config.httpShouldSetCookies = false
        config.httpCookieAcceptPolicy = .never
        if let protocolClasses { config.protocolClasses = protocolClasses }
        configuration = config
    }

    public func listTools(accessToken: String) async throws -> DecodedJSONValue {
        try await send(method: "tools/list", params: .object([:]), token: accessToken)
    }

    public func callTool(
        name: String,
        arguments: [String: DecodedJSONValue],
        accessToken: String
    ) async throws -> ToolResult {
        let params: DecodedJSONValue = .object([
            "name": .string(name), "arguments": .object(arguments)
        ])
        let result: ToolResult = try await send(method: "tools/call", params: params, token: accessToken)
        if result.isError == true {
            for content in result.content {
                guard case let .object(block) = content,
                      case let .string(text)? = block["text"],
                      let data = text.data(using: .utf8),
                      let value = try? JSONDecoder().decode(DecodedJSONValue.self, from: data),
                      case let .object(body) = value,
                      case let .object(error)? = body["error"],
                      case let .string(code)? = error["code"], code == "RATE_LIMIT"
                else { continue }
                let seconds: Int? = if case let .number(value)? = error["retryAfterSeconds"],
                                       value > 0, value < Double(Int.max), value.rounded() == value {
                    Int(value)
                } else { nil }
                throw Failure.rateLimited(retryAfterSeconds: seconds)
            }
        }
        return result
    }

    private func send<Result: Decodable & Sendable>(
        method: String,
        params: DecodedJSONValue,
        token: String
    ) async throws -> Result {
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
        request.httpBody = try JSONEncoder().encode(body)
        let transport = ResponseLinesTransport(followRedirects: false)
        defer { transport.cancel() }
        let responseLines = try await transport.start(request: request, configuration: configuration)
        let response = responseLines.response
        guard let http = response as? HTTPURLResponse else { throw Failure.invalidResponse }
        if http.statusCode == 401 { throw Failure.unauthorized }
        if http.statusCode == 429 {
            throw Failure.rateLimited(retryAfterSeconds: Self.retryDelay(
                http.value(forHTTPHeaderField: "Retry-After")
            ))
        }
        guard http.statusCode == 200 else { throw Failure.httpStatus(http.statusCode) }
        return try await MCPResponseReader.read(
            responseLines.lines,
            contentType: http.value(forHTTPHeaderField: "Content-Type"), id: id
        )
    }

    private static func retryDelay(_ value: String?) -> Int? {
        guard let value else { return nil }
        if let seconds = Int(value), seconds > 0 { return seconds }
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.timeZone = TimeZone(secondsFromGMT: 0)
        formatter.dateFormat = "EEE, dd MMM yyyy HH:mm:ss 'GMT'"
        guard let date = formatter.date(from: value) else { return nil }
        let seconds = date.timeIntervalSinceNow
        guard seconds > 0, seconds < Double(Int.max) else { return nil }
        return Int(ceil(seconds))
    }
}
