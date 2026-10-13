import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAPI
import VouchaModels
import XCTest

final class MemberMCPClientTests: XCTestCase {
    func testSSEAnswersPingAndClosesSession() async throws {
        let events = "data: {\"jsonrpc\":\"2.0\",\"id\":17,\"method\":\"ping\"}\n\n" +
            "data: {\"jsonrpc\":\"2.0\",\"id\":\"2\",\"result\":{\"tools\":[]}}\n\n"
        MCPTestURLProtocol.configure(data: Data(events.utf8), status: 200, contentType: "Text/Event-Stream")
        let client = try MemberMCPClient(
            siteOrigin: XCTUnwrap(URL(string: "https://example.test")),
            protocolClasses: [MCPTestURLProtocol.self]
        )
        _ = try await client.listTools(accessToken: "access-1")
        let reply = try XCTUnwrap(MCPTestURLProtocol.serverReply())
        let fields = try XCTUnwrap(JSONSerialization.jsonObject(with: reply) as? [String: Any])
        XCTAssertEqual(fields["id"] as? Int, 17)
        XCTAssertNotNil(fields["result"])

        try await client.close(accessToken: "access-1")
        XCTAssertEqual(MCPTestURLProtocol.methodHistory().last, "DELETE")
        XCTAssertEqual(MCPTestURLProtocol.closedSession(), "test-session")
        do {
            _ = try await client.listTools(accessToken: "access-1")
            XCTFail("A closed MCP client must not open a new session")
        } catch MemberMCPClient.Failure.closed {}
    }

    func testInitializationSSEPingUsesReturnedSessionHeader() async throws {
        let initEvents = "data: {\"jsonrpc\":\"2.0\",\"id\":17,\"method\":\"ping\"}\n\n" +
            "data: {\"jsonrpc\":\"2.0\",\"id\":\"1\",\"result\":{\"protocolVersion\":\"2025-06-18\"}}\n\n"
        MCPTestURLProtocol.configure(
            data: Data(#"{"jsonrpc":"2.0","id":"2","result":{"tools":[]}}"#.utf8),
            status: 200, initializeEvents: Data(initEvents.utf8)
        )
        let client = try MemberMCPClient(
            siteOrigin: XCTUnwrap(URL(string: "https://example.test")),
            protocolClasses: [MCPTestURLProtocol.self]
        )

        _ = try await client.listTools(accessToken: "access-1")

        XCTAssertNotNil(MCPTestURLProtocol.serverReply())
        XCTAssertEqual(MCPTestURLProtocol.serverReplySession(), "test-session")
    }

    func testToolListingForwardsOpaqueCursor() async throws {
        MCPTestURLProtocol.configure(
            data: Data(#"{"jsonrpc":"2.0","id":"2","result":{"tools":[],"nextCursor":"next/+"}}"#.utf8),
            status: 200
        )
        let client = try MemberMCPClient(
            siteOrigin: XCTUnwrap(URL(string: "https://example.test")),
            protocolClasses: [MCPTestURLProtocol.self]
        )

        let result = try await client.listTools(accessToken: "access-1", cursor: "opaque/+")

        let body = try XCTUnwrap(MCPTestURLProtocol.bodySnapshot())
        let rpc = try XCTUnwrap(JSONSerialization.jsonObject(with: body) as? [String: Any])
        let params = try XCTUnwrap(rpc["params"] as? [String: Any])
        XCTAssertEqual(params["cursor"] as? String, "opaque/+")
        if case let .object(fields) = result, case let .string(next)? = fields["nextCursor"] {
            XCTAssertEqual(next, "next/+")
        } else {
            XCTFail("The next cursor was not returned")
        }
    }

    override func tearDown() {
        MCPTestURLProtocol.configure(data: Data(), status: 200)
        super.tearDown()
    }

    func testCallUsesBearerOnlyMCPAndPreservesWrappedAndStructuredContent() async throws {
        MCPTestURLProtocol.configure(data: Data(#"""
        {
        "jsonrpc":"2.0","id":"2","result":{
          "content":[{"type":"text","text":"<untrusted>member text</untrusted>"}],
          "structuredContent":{"post_id":"post-1"},"isError":false
        }}
        """#.utf8), status: 200)
        let client = try MemberMCPClient(
            siteOrigin: XCTUnwrap(URL(string: "https://example.test")),
            protocolClasses: [MCPTestURLProtocol.self]
        )

        let result = try await client.callTool(
            name: "read_post", arguments: ["id": .string("post-1")], accessToken: "access-1"
        )

        let request = try XCTUnwrap(MCPTestURLProtocol.snapshot())
        XCTAssertEqual(request.url?.absoluteString, "https://example.test/api/v1/mcp")
        XCTAssertEqual(request.value(forHTTPHeaderField: "Authorization"), "Bearer access-1")
        XCTAssertNil(request.value(forHTTPHeaderField: "Cookie"))
        XCTAssertEqual(request.value(forHTTPHeaderField: "Accept"), "application/json, text/event-stream")
        XCTAssertEqual(request.value(forHTTPHeaderField: "MCP-Protocol-Version"), "2025-06-18")
        XCTAssertEqual(request.value(forHTTPHeaderField: "Mcp-Session-Id"), "test-session")
        XCTAssertEqual(MCPTestURLProtocol.methodHistory(), ["initialize", "notifications/initialized", "tools/call"])
        let body = try XCTUnwrap(MCPTestURLProtocol.bodySnapshot())
        let object = try XCTUnwrap(JSONSerialization.jsonObject(with: body) as? [String: Any])
        XCTAssertEqual(object["method"] as? String, "tools/call")
        let params = try XCTUnwrap(object["params"] as? [String: Any])
        XCTAssertEqual(params["name"] as? String, "read_post")
        let content = try XCTUnwrap(result.content.first)
        if case let .object(fields) = content, case let .string(text)? = fields["text"] {
            XCTAssertEqual(text, "<untrusted>member text</untrusted>")
        } else {
            XCTFail("MCP wrapped content was changed")
        }
        if case let .object(fields)? = result.structuredContent,
           case let .string(postId)? = fields["post_id"] {
            XCTAssertEqual(postId, "post-1")
        } else {
            XCTFail("MCP structured content was lost")
        }
    }

    func testSSEWaitsForCorrelatedResultAfterNotifications() async throws {
        let events = """
        : heartbeat

        data: {"jsonrpc":"2.0","method":"notifications/progress"}

        data: {"jsonrpc":"2.0","id":"other","result":{}}

        data: {"jsonrpc":"2.0","id":"2","result":{"tools":[{"name":"read_post"}]}}

        """
        MCPTestURLProtocol.configure(data: Data(events.utf8), status: 200, contentType: "text/event-stream")
        let client = try MemberMCPClient(
            siteOrigin: XCTUnwrap(URL(string: "https://example.test")),
            protocolClasses: [MCPTestURLProtocol.self]
        )

        let result = try await client.listTools(accessToken: "access-1")

        if case let .object(fields) = result,
           case let .array(tools)? = fields["tools"],
           case let .object(tool)? = tools.first,
           case let .string(name)? = tool["name"] {
            XCTAssertEqual(name, "read_post")
        } else {
            XCTFail("The matching SSE result was not decoded")
        }
    }

    func testSSECommentsWithoutResultStopAtAbsoluteDeadline() async throws {
        MCPTestURLProtocol.configure(
            data: Data(": heartbeat\n\n".utf8), status: 200, contentType: "text/event-stream",
            holdToolResponse: true
        )
        let client = try MemberMCPClient(
            siteOrigin: XCTUnwrap(URL(string: "https://example.test")),
            protocolClasses: [MCPTestURLProtocol.self], responseTimeout: 0.05
        )

        do {
            _ = try await client.listTools(accessToken: "access-1")
            XCTFail("An SSE stream without the correlated result must time out")
        } catch {
            XCTAssertTrue(MCPTestURLProtocol.stoppedHeldResponse())
        }
    }

    func testSSEIgnoresUnmatchedResultWithDifferentToolShape() async throws {
        let events = """
        data: {"jsonrpc":"2.0","id":"other","result":{"unrelated":true}}

        data: {"jsonrpc":"2.0","id":"2","result":{"content":[],"isError":false}}

        """
        MCPTestURLProtocol.configure(data: Data(events.utf8), status: 200, contentType: "text/event-stream")
        let client = try MemberMCPClient(
            siteOrigin: XCTUnwrap(URL(string: "https://example.test")),
            protocolClasses: [MCPTestURLProtocol.self]
        )

        let result = try await client.callTool(name: "read_post", arguments: [:], accessToken: "access-1")
        XCTAssertTrue(result.content.isEmpty)
        XCTAssertEqual(result.isError, false)
    }

    func testHttpRateLimitKeepsRetryAfter() async throws {
        MCPTestURLProtocol.configure(data: Data(), status: 429, retryAfter: "45")
        let client = try MemberMCPClient(
            siteOrigin: XCTUnwrap(URL(string: "https://example.test")),
            protocolClasses: [MCPTestURLProtocol.self]
        )
        do {
            let _: DecodedJSONValue = try await client.listTools(accessToken: "access-1")
            XCTFail("HTTP 429 must not become an empty list")
        } catch let MemberMCPClient.Failure.rateLimited(seconds) {
            XCTAssertEqual(seconds, 45)
        }
    }

    func testInBandRateLimitKeepsRetryAfter() async throws {
        let inner = #"{"error":{"status":429,"code":"RATE_LIMIT","retryAfterSeconds":17}}"#
        let wrapped = try JSONSerialization.data(withJSONObject: [
            "jsonrpc": "2.0", "id": "2",
            "result": ["isError": true, "content": [["type": "text", "text": inner]]]
        ])
        MCPTestURLProtocol.configure(data: wrapped, status: 200)
        let client = try MemberMCPClient(
            siteOrigin: XCTUnwrap(URL(string: "https://example.test")),
            protocolClasses: [MCPTestURLProtocol.self]
        )
        do {
            _ = try await client.callTool(name: "read_post", arguments: [:], accessToken: "access-1")
            XCTFail("An in-band rate refusal must not be forwarded as a successful tool result")
        } catch let MemberMCPClient.Failure.rateLimited(seconds) {
            XCTAssertEqual(seconds, 17)
        }
    }

    func testDiscoveryReadsBothMetadataDocumentsAndRejectsMismatchedResource() async throws {
        let resource = Data(
            #"{"resource":"https://example.test/api/v1/mcp","authorization_servers":["https://example.test"]}"#
                .utf8
        )
        let server = Data(
            #"{"issuer":"https://example.test","authorization_endpoint":"https://example.test/authorize","token_endpoint":"https://example.test/token","revocation_endpoint":"https://example.test/revoke"}"#
                .utf8
        )
        MCPTestURLProtocol.configureMetadata(resource: resource, server: server)
        let discovery = try MemberMCPOAuthDiscovery(
            siteOrigin: XCTUnwrap(URL(string: "https://example.test")),
            protocolClasses: [MCPTestURLProtocol.self]
        )

        let metadata = try await discovery.discover()

        XCTAssertEqual(metadata.issuerIdentifier, "https://example.test")
        XCTAssertEqual(metadata.tokenEndpoint.absoluteString, "https://example.test/token")
        XCTAssertEqual(MCPTestURLProtocol.paths(), [
            "/.well-known/oauth-protected-resource/api/v1/mcp",
            "/.well-known/oauth-authorization-server"
        ])
        MCPTestURLProtocol.configureMetadata(
            resource: Data(
                #"{"resource":"https://example.test/api/v1/admin/mcp","authorization_servers":["https://example.test"]}"#
                    .utf8
            ),
            server: server
        )
        let rejected = try MemberMCPOAuthDiscovery(
            siteOrigin: XCTUnwrap(URL(string: "https://example.test")),
            protocolClasses: [MCPTestURLProtocol.self]
        )
        do {
            _ = try await rejected.discover()
            XCTFail("A different protected resource must fail closed")
        } catch MemberMCPOAuthDiscovery.Failure.invalidMetadata {}
    }

    func testPathIssuerInsertsWellKnownBeforeIssuerPathAndRejectsQueryIssuer() async throws {
        let resource = Data(
            #"{"resource":"https://example.test/api/v1/mcp","authorization_servers":["https://identity.test/tenant"]}"#
                .utf8
        )
        let server = Data(
            #"{"issuer":"https://identity.test/tenant","authorization_endpoint":"https://identity.test/authorize","token_endpoint":"https://identity.test/token","revocation_endpoint":"https://identity.test/revoke"}"#
                .utf8
        )
        MCPTestURLProtocol.configureMetadata(
            resource: resource, server: server,
            serverPath: "/.well-known/oauth-authorization-server/tenant"
        )
        let discovery = try MemberMCPOAuthDiscovery(
            siteOrigin: XCTUnwrap(URL(string: "https://example.test")),
            protocolClasses: [MCPTestURLProtocol.self]
        )
        let metadata = try await discovery.discover()
        XCTAssertEqual(metadata.issuerIdentifier, "https://identity.test/tenant")
        XCTAssertEqual(MCPTestURLProtocol.paths()[1], "/.well-known/oauth-authorization-server/tenant")

        MCPTestURLProtocol.configureMetadata(
            resource: Data(
                #"{"resource":"https://example.test/api/v1/mcp","authorization_servers":["https://identity.test/tenant?bad=1"]}"#
                    .utf8
            ),
            server: server
        )
        do {
            _ = try await discovery.discover()
            XCTFail("Issuer query must be rejected before metadata fetch")
        } catch MemberMCPOAuthDiscovery.Failure.invalidMetadata {}
    }

    func testHttpDateRateLimitProducesPositiveDelay() async throws {
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.timeZone = TimeZone(secondsFromGMT: 0)
        formatter.dateFormat = "EEE, dd MMM yyyy HH:mm:ss 'GMT'"
        MCPTestURLProtocol.configure(
            data: Data(), status: 429, retryAfter: formatter.string(from: Date().addingTimeInterval(120))
        )
        let client = try MemberMCPClient(
            siteOrigin: XCTUnwrap(URL(string: "https://example.test")),
            protocolClasses: [MCPTestURLProtocol.self]
        )
        do {
            let _: DecodedJSONValue = try await client.listTools(accessToken: "access-1")
            XCTFail("HTTP 429 must not become an empty list")
        } catch let MemberMCPClient.Failure.rateLimited(seconds) {
            XCTAssertGreaterThan(seconds ?? 0, 0)
            XCTAssertLessThanOrEqual(seconds ?? 0, 120)
        }
    }
}

private final class MCPTestURLProtocol: URLProtocol {
    private static let lock = NSLock()
    private static var data = Data()
    private static var status = 200
    private static var retryAfter: String?
    private static var contentType = "application/json"
    private static var captured: URLRequest?
    private static var capturedBody: Data?
    private static var metadata: [String: Data] = [:]
    private static var requestedPaths: [String] = []
    private static var methods: [String] = []
    private static var replyBody: Data?
    private static var deletedSession: String?
    private static var replySession: String?
    private static var initializeEvents: Data?
    private static var holdToolResponse = false
    private static var heldResponseStopped = false

    static func configure(
        data: Data, status: Int, retryAfter: String? = nil,
        contentType: String = "application/json", initializeEvents: Data? = nil,
        holdToolResponse: Bool = false
    ) {
        lock.lock()
        self.data = data
        self.status = status
        self.retryAfter = retryAfter
        self.contentType = contentType
        self.initializeEvents = initializeEvents
        self.holdToolResponse = holdToolResponse
        heldResponseStopped = false
        captured = nil
        capturedBody = nil
        metadata = [:]
        requestedPaths = []
        methods = []
        replyBody = nil
        replySession = nil
        deletedSession = nil
        lock.unlock()
    }

    static func configureMetadata(
        resource: Data, server: Data,
        serverPath: String = "/.well-known/oauth-authorization-server"
    ) {
        lock.lock()
        metadata = [
            "/.well-known/oauth-protected-resource/api/v1/mcp": resource,
            serverPath: server
        ]
        requestedPaths = []
        lock.unlock()
    }

    static func paths() -> [String] {
        lock.lock()
        defer { lock.unlock() }
        return requestedPaths
    }

    static func methodHistory() -> [String] {
        lock.lock()
        defer { lock.unlock() }
        return methods
    }

    static func serverReply() -> Data? {
        lock.lock()
        defer { lock.unlock() }
        return replyBody
    }

    static func serverReplySession() -> String? {
        lock.lock()
        defer { lock.unlock() }
        return replySession
    }

    static func closedSession() -> String? {
        lock.lock()
        defer { lock.unlock() }
        return deletedSession
    }

    static func stoppedHeldResponse() -> Bool {
        lock.lock()
        defer { lock.unlock() }
        return heldResponseStopped
    }

    static func snapshot() -> URLRequest? {
        lock.lock()
        defer { lock.unlock() }
        return captured
    }

    static func bodySnapshot() -> Data? {
        lock.lock()
        defer { lock.unlock() }
        return capturedBody
    }

    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        Self.lock.lock()
        let body = request.httpBody ?? request.httpBodyStream.map(Self.readBodyStream)
        let rpc = body.flatMap { (try? JSONSerialization.jsonObject(with: $0)) as? [String: Any] }
        let method = rpc?["method"] as? String
        if let method { Self.methods.append(method) }
        if request.httpMethod == "DELETE" {
            Self.methods.append("DELETE")
            Self.deletedSession = request.value(forHTTPHeaderField: "Mcp-Session-Id")
        } else if method == nil, rpc?["result"] != nil {
            Self.replyBody = body
            Self.replySession = request.value(forHTTPHeaderField: "Mcp-Session-Id")
        }
        if method != "initialize", method != "notifications/initialized",
           request.httpMethod != "DELETE", rpc?["result"] == nil {
            Self.captured = request
            Self.capturedBody = body
        }
        let data = Self.data
        let path = request.url?.path ?? ""
        Self.requestedPaths.append(path)
        let selected = method == "initialize"
            ? Self
            .initializeEvents ?? Data(#"{"jsonrpc":"2.0","id":"1","result":{"protocolVersion":"2025-06-18"}}"#.utf8)
            : method == "notifications/initialized" || request.httpMethod == "DELETE" ||
            rpc?["result"] != nil ? Data() : Self.metadata[path] ?? data
        let status = method == "notifications/initialized" || rpc?["result"] != nil
            ? 202 : request.httpMethod == "DELETE" ? 204 : method == "initialize" ? 200 : Self.status
        let retryAfter = Self.retryAfter
        let holdToolResponse = Self.holdToolResponse && method == "tools/list"
        let contentType = method == "initialize" && Self.initializeEvents != nil
            ? "text/event-stream" : method == "initialize" || method == "notifications/initialized"
            ? "application/json" : Self.contentType
        Self.lock.unlock()
        var headers = ["Content-Type": contentType]
        if method == "initialize" { headers["Mcp-Session-Id"] = "test-session" }
        if let retryAfter { headers["Retry-After"] = retryAfter }
        let response = HTTPURLResponse(
            url: request.url!, statusCode: status, httpVersion: "HTTP/1.1", headerFields: headers
        )!
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: selected)
        if !holdToolResponse { client?.urlProtocolDidFinishLoading(self) }
    }

    override func stopLoading() {
        Self.lock.lock()
        if Self.holdToolResponse { Self.heldResponseStopped = true }
        Self.lock.unlock()
    }

    private static func readBodyStream(_ stream: InputStream) -> Data {
        stream.open()
        defer { stream.close() }
        var data = Data()
        var bytes = [UInt8](repeating: 0, count: 4_096)
        while stream.hasBytesAvailable {
            let count = stream.read(&bytes, maxLength: bytes.count)
            if count <= 0 { break }
            data.append(bytes, count: count)
        }
        return data
    }
}
