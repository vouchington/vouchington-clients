import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAPI
import VouchaTestSupport
import XCTest

final class MemberMCPTransportRejectionTests: XCTestCase {
    override func tearDown() {
        MCPTransportRejectionURLProtocol.reset()
        super.tearDown()
    }

    func testToolListingUnauthorizedUsesSharedRejection() async {
        MCPTransportRejectionURLProtocol.configure(rpcStatus: 401)
        await assertFailure(.unauthorized)
    }

    func testToolListingMissingSessionExpiresCurrentSession() async {
        MCPTransportRejectionURLProtocol.configure(rpcStatus: 404)
        await assertFailure(.sessionExpired)
    }

    func testInitializedNotificationUnauthorizedUsesSharedRejection() async {
        MCPTransportRejectionURLProtocol.configure(initializedStatus: 401)
        await assertFailure(.unauthorized)
    }

    func testServerReplyUnauthorizedUsesSharedRejection() async {
        MCPTransportRejectionURLProtocol.configure(includePing: true, replyStatus: 401)
        await assertFailure(.unauthorized)
    }

    func testServerReplyMissingSessionExpiresCurrentSession() async {
        MCPTransportRejectionURLProtocol.configure(includePing: true, replyStatus: 404)
        await assertFailure(.sessionExpired)
    }

    private func assertFailure(_ expected: MemberMCPClient.Failure) async {
        do {
            let client = try MemberMCPClient(
                siteOrigin: XCTUnwrap(URL(string: "https://example.test")),
                protocolClasses: [MCPTransportRejectionURLProtocol.self]
            )
            _ = try await client.listTools(accessToken: "access-1")
            XCTFail("MCP transport must reject the listing")
        } catch let failure as MemberMCPClient.Failure {
            XCTAssertEqual(String(describing: failure), String(describing: expected))
        } catch {
            XCTFail("Unexpected MCP transport failure: \(error)")
        }
    }
}

final class MCPTransportRejectionURLProtocol: URLProtocol {
    private struct State {
        var rpcStatus = 200
        var initializedStatus = 202
        var replyStatus = 202
        var includePing = false
    }

    private static let lock = NSLock()
    private static var state = State()
    private let deliveryLock = NSRecursiveLock()
    private var stopped = false

    static func configure(
        rpcStatus: Int = 200,
        initializedStatus: Int = 202,
        includePing: Bool = false,
        replyStatus: Int = 202
    ) {
        lock.lock()
        defer { lock.unlock() }
        state = State(
            rpcStatus: rpcStatus,
            initializedStatus: initializedStatus,
            replyStatus: replyStatus,
            includePing: includePing
        )
    }

    static func reset() {
        configure()
    }

    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        let body = URLRequestHTTPBody.data(from: request)
        let rpc = body.flatMap { try? JSONSerialization.jsonObject(with: $0) as? [String: Any] } ?? [:]
        let method = rpc["method"] as? String
        let requestId = rpc["id"] as? String ?? "1"
        let isReply = method == nil && rpc["result"] != nil
        Self.lock.lock()
        let state = Self.state
        Self.lock.unlock()
        var status = 200
        var headers = ["Content-Type": "application/json", "Mcp-Session-Id": "test-session"]
        var data = Data()
        if method == "initialize" {
            data = Data(
                #"{"jsonrpc":"2.0","id":"\#(requestId)","result":{"protocolVersion":"2025-06-18"}}"#.utf8
            )
        } else if method == "notifications/initialized" {
            status = state.initializedStatus
        } else if isReply {
            status = state.replyStatus
        } else if method == "tools/list" {
            status = state.rpcStatus
            if state.includePing {
                headers["Content-Type"] = "text/event-stream"
                let ping = #"data: {"jsonrpc":"2.0","id":17,"method":"ping"}"#
                let result = #"data: {"jsonrpc":"2.0","id":"\#(requestId)","result":{"tools":[]}}"#
                data = Data("\(ping)\n\n\(result)\n\n".utf8)
            } else {
                data = Data(
                    #"{"jsonrpc":"2.0","id":"\#(requestId)","result":{"tools":[]}}"#.utf8
                )
            }
        } else {
            status = 500
        }
        deliver(status: status, headers: headers, data: data)
    }

    private func deliver(status: Int, headers: [String: String], data: Data) {
        deliveryLock.lock()
        defer { deliveryLock.unlock() }
        guard !stopped else { return }
        let response = HTTPURLResponse(
            url: request.url!, statusCode: status, httpVersion: "HTTP/1.1", headerFields: headers
        )!
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: data)
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {
        deliveryLock.lock()
        defer { deliveryLock.unlock() }
        stopped = true
    }
}
