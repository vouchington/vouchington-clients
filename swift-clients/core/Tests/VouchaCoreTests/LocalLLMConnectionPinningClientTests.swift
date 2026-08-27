#if canImport(CFNetwork)
    import CFNetwork
#endif
import Foundation
@testable import VouchaCore
import XCTest

final class LocalLLMConnectionPinningClientTests: XCTestCase {
    override func setUp() {
        super.setUp()
        PinningURLProtocol.reset()
        ResponsesURLProtocol.responseData = Data(#"{"output_text":"Local answer"}"#.utf8)
        ResponsesURLProtocol.statusCode = 200
        ResponsesURLProtocol.capturedRequest = nil
        ResponsesURLProtocol.capturedBody = nil
    }

    func testClientRefusesPublicResolvedAddressesBeforeHTTP() async {
        let client = OpenAICompatibleResponsesClient(
            protocolClasses: [PinningURLProtocol.self],
            addressResolver: StubAddressResolver(addresses: [ip("8.8.8.8")])
        )
        do {
            _ = try await client.generateAssistantResponse(
                message: "Hello",
                history: [],
                endpoint: endpoint("http://model-box.local:11434/v1"),
                apiKey: "secret"
            )
            XCTFail("Expected the public resolved address to be refused")
        } catch {
            XCTAssertEqual(
                error as? LocalLLMError,
                .resolvedAddressNotPrivate(host: "model-box.local")
            )
        }
        XCTAssertEqual(PinningURLProtocol.loadCount, 0)
    }

    func testProtocolClassesStillPinCleartextRequests() async {
        let client = OpenAICompatibleResponsesClient(
            protocolClasses: [PinningURLProtocol.self],
            addressResolver: StubAddressResolver(addresses: [ip("8.8.8.8")])
        )
        _ = try? await client.generateAssistantResponse(
            message: "Hello",
            history: [],
            endpoint: endpoint("http://nas.local:11434/v1"),
            apiKey: nil
        )
        XCTAssertEqual(PinningURLProtocol.loadCount, 0)
        XCTAssertNotEqual(PinningURLProtocol.capturedHost, "nas.local")
    }

    func testClientRewritesAPrivateResolvedAddressAndPreservesTheOriginalHost() async throws {
        PinningURLProtocol.responseData = Data(#"{"output_text":"Pinned"}"#.utf8)
        let client = OpenAICompatibleResponsesClient(
            protocolClasses: [PinningURLProtocol.self],
            addressResolver: StubAddressResolver(addresses: [ip("192.168.1.20")])
        )
        let response = try await client.generateAssistantResponse(
            message: "Hello",
            history: [],
            endpoint: endpoint("http://model-box.local:11434/v1"),
            apiKey: nil
        )
        XCTAssertEqual(response, "Pinned")
        XCTAssertEqual(PinningURLProtocol.capturedURL?.host, "192.168.1.20")
        XCTAssertEqual(PinningURLProtocol.capturedHostHeader, "model-box.local:11434")
    }

    func testClientDoesNotRewriteHTTPS() async throws {
        PinningURLProtocol.responseData = Data(#"{"output_text":"Secure"}"#.utf8)
        let client = OpenAICompatibleResponsesClient(
            protocolClasses: [PinningURLProtocol.self],
            addressResolver: StubAddressResolver(addresses: [ip("8.8.8.8")])
        )
        let response = try await client.generateAssistantResponse(
            message: "Hello",
            history: [],
            endpoint: endpoint("https://models.example.test/v1"),
            apiKey: nil
        )
        XCTAssertEqual(response, "Secure")
        XCTAssertEqual(PinningURLProtocol.capturedURL?.host, "models.example.test")
        XCTAssertEqual(PinningURLProtocol.capturedURL?.scheme, "https")
    }

    func testIPLiteralSkipsTheResolver() async throws {
        PinningURLProtocol.responseData = Data(#"{"output_text":"Literal"}"#.utf8)
        let resolver = StubAddressResolver(addresses: [ip("8.8.8.8")])
        let client = OpenAICompatibleResponsesClient(
            protocolClasses: [PinningURLProtocol.self],
            addressResolver: resolver
        )
        let response = try await client.generateAssistantResponse(
            message: "Hello",
            history: [],
            endpoint: endpoint("http://127.0.0.1:11434/v1"),
            apiKey: nil
        )
        XCTAssertEqual(response, "Literal")
        XCTAssertFalse(resolver.wasCalled)
        XCTAssertEqual(PinningURLProtocol.capturedURL?.host, "127.0.0.1")
    }

    func testImmediateConnectFallbackUsesTheNextPrivateAddress() async throws {
        PinningURLProtocol.responseData = Data(#"{"output_text":"Second"}"#.utf8)
        PinningURLProtocol.failingHosts = ["10.0.0.5"]
        let client = OpenAICompatibleResponsesClient(
            protocolClasses: [PinningURLProtocol.self],
            addressResolver: StubAddressResolver(addresses: [ip("10.0.0.5"), ip("192.168.1.1")]),
            connectProbe: StubConnectProbe(refusals: ["10.0.0.5"])
        )
        let response = try await client.generateAssistantResponse(
            message: "Hello",
            history: [],
            endpoint: endpoint("http://model-box.local:11434/v1"),
            apiKey: nil
        )
        XCTAssertEqual(response, "Second")
        XCTAssertEqual(PinningURLProtocol.capturedURL?.host, "192.168.1.1")
        XCTAssertEqual(PinningURLProtocol.loadCount, 1)
    }

    func testALiveFirstCandidateIsNotAbandonedAfterFiveSeconds() async throws {
        PinningURLProtocol.holdOpen = true
        let client = OpenAICompatibleResponsesClient(
            protocolClasses: [PinningURLProtocol.self],
            addressResolver: StubAddressResolver(addresses: [ip("10.0.0.5"), ip("192.168.1.1")]),
            connectProbe: StubConnectProbe()
        )
        let task = Task {
            try await client.generateAssistantResponse(
                message: "Hello",
                history: [],
                endpoint: endpoint("http://model-box.local:11434/v1"),
                apiKey: nil
            )
        }
        try await waitUntil { PinningURLProtocol.loadCount == 1 }
        try await Task.sleep(nanoseconds: 5_200_000_000)
        XCTAssertEqual(PinningURLProtocol.loadCount, 1)
        XCTAssertEqual(PinningURLProtocol.capturedURL?.host, "10.0.0.5")
        task.cancel()
        do {
            _ = try await task.value
            XCTFail("Expected cancellation")
        } catch is CancellationError {
            // Expected
        } catch {
            XCTFail("Expected CancellationError, got \(error)")
        }
    }

    func testAllCandidatesFailingDoesNotLookLikeARedirect() async {
        PinningURLProtocol.failingHosts = ["10.0.0.5", "192.168.1.1"]
        let client = OpenAICompatibleResponsesClient(
            protocolClasses: [PinningURLProtocol.self],
            addressResolver: StubAddressResolver(addresses: [ip("10.0.0.5"), ip("192.168.1.1")]),
            connectProbe: StubConnectProbe()
        )
        do {
            _ = try await client.generateAssistantResponse(
                message: "Hello",
                history: [],
                endpoint: endpoint("http://model-box.local:11434/v1"),
                apiKey: nil
            )
            XCTFail("Expected a connect failure")
        } catch {
            XCTAssertEqual(error as? LocalLLMError, .unableToConnect(host: "model-box.local"))
        }
        XCTAssertEqual(PinningURLProtocol.loadCount, 2)
    }

    func testDNSFailureNeverStartsHTTP() async {
        let client = OpenAICompatibleResponsesClient(
            protocolClasses: [PinningURLProtocol.self],
            addressResolver: FailingAddressResolver()
        )
        do {
            _ = try await client.generateAssistantResponse(
                message: "Hello",
                history: [],
                endpoint: endpoint("http://model-box.local:11434/v1"),
                apiKey: nil
            )
            XCTFail("Expected DNS failure")
        } catch {
            XCTAssertEqual(error as? LocalLLMError, .dnsResolutionFailed)
        }
        XCTAssertEqual(PinningURLProtocol.loadCount, 0)
    }

    func testCallerCancellationDuringResolveDoesNotStartHTTP() async {
        let client = OpenAICompatibleResponsesClient(
            protocolClasses: [PinningURLProtocol.self],
            addressResolver: HangingAddressResolver()
        )
        let task = Task {
            try await client.generateAssistantResponse(
                message: "Hello",
                history: [],
                endpoint: endpoint("http://model-box.local:11434/v1"),
                apiKey: nil
            )
        }
        task.cancel()
        do {
            _ = try await task.value
            XCTFail("Expected cancellation")
        } catch is CancellationError {
            // Expected
        } catch {
            XCTFail("Expected CancellationError, got \(error)")
        }
        XCTAssertEqual(PinningURLProtocol.loadCount, 0)
    }

    #if canImport(Darwin)
        func testSessionConfigurationDisablesHTTPProxy() {
            let configuration = OpenAICompatibleResponsesClient.makeSessionConfiguration(
                protocolClasses: nil
            )
            let proxy = configuration.connectionProxyDictionary ?? [:]
            let enabled = proxy[kCFNetworkProxiesHTTPEnable as String]
            XCTAssertTrue(
                enabled as? Bool == false || enabled as? Int == 0 || enabled as? NSNumber == 0,
                "HTTP proxy should be disabled, got \(String(describing: enabled))"
            )
        }
    #endif

    private func endpoint(_ url: String) -> LocalLLMEndpointProfile {
        LocalLLMEndpointProfile(
            endpoint: url,
            modelNames: ["gpt-oss"],
            selectedModelName: "gpt-oss"
        )
    }

    private func ip(_ textual: String) -> LocalLLMIPAddress {
        LocalLLMIPAddress.parse(textual)!
    }

    private func waitUntil(
        timeout: TimeInterval = 1,
        predicate: () -> Bool
    ) async throws {
        let deadline = Date().addingTimeInterval(timeout)
        while !predicate() {
            if Date() > deadline {
                throw NSError(domain: "waitUntil", code: 1)
            }
            try await Task.sleep(nanoseconds: 10_000_000)
        }
    }
}

private final class StubAddressResolver: LocalLLMAddressResolving, @unchecked Sendable {
    private let addresses: [LocalLLMIPAddress]
    private(set) var wasCalled = false

    init(addresses: [LocalLLMIPAddress]) {
        self.addresses = addresses
    }

    func addresses(for _: String) async throws -> [LocalLLMIPAddress] {
        wasCalled = true
        return addresses
    }
}

private struct FailingAddressResolver: LocalLLMAddressResolving {
    func addresses(for _: String) async throws -> [LocalLLMIPAddress] {
        throw LocalLLMError.dnsResolutionFailed
    }
}

private struct HangingAddressResolver: LocalLLMAddressResolving {
    func addresses(for _: String) async throws -> [LocalLLMIPAddress] {
        try await Task.sleep(nanoseconds: 30_000_000_000)
        return []
    }
}

private struct StubConnectProbe: LocalLLMConnectProbing {
    var refusals: Set<String> = []

    func canConnect(to address: LocalLLMIPAddress, port _: Int, timeout _: TimeInterval) async throws
        -> Bool {
        !refusals.contains(address.connectAddress.textual)
    }
}

private final class PinningURLProtocol: URLProtocol {
    private static let lock = NSLock()
    private static var _responseData = Data()
    private static var _failingHosts: Set<String> = []
    private static var _holdOpen = false
    private static var _loadCount = 0
    private static var _capturedURL: URL?
    private static var _capturedHostHeader: String?
    private let stateLock = NSLock()
    private var stopped = false

    static var responseData: Data {
        get { locked { _responseData } }
        set { locked { _responseData = newValue } }
    }

    static var failingHosts: Set<String> {
        get { locked { _failingHosts } }
        set { locked { _failingHosts = newValue } }
    }

    static var holdOpen: Bool {
        get { locked { _holdOpen } }
        set { locked { _holdOpen = newValue } }
    }

    static var loadCount: Int {
        locked { _loadCount }
    }

    static var capturedURL: URL? {
        locked { _capturedURL }
    }

    static var capturedHostHeader: String? {
        locked { _capturedHostHeader }
    }

    static var capturedHost: String? {
        capturedURL?.host
    }

    static func reset() {
        locked {
            _responseData = Data()
            _failingHosts = []
            _holdOpen = false
            _loadCount = 0
            _capturedURL = nil
            _capturedHostHeader = nil
        }
    }

    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        let host = request.url?.host ?? ""
        Self.locked {
            Self._loadCount += 1
            Self._capturedURL = self.request.url
            Self._capturedHostHeader = self.request.value(forHTTPHeaderField: "Host")
        }
        if Self.failingHosts.contains(host) {
            deliverIfNotStopped {
                self.client?.urlProtocol(self, didFailWithError: URLError(.cannotConnectToHost))
            }
            return
        }
        if Self.holdOpen {
            return
        }
        deliverIfNotStopped {
            guard let url = self.request.url,
                  let response = HTTPURLResponse(
                      url: url,
                      statusCode: 200,
                      httpVersion: nil,
                      headerFields: ["Content-Type": "application/json"]
                  )
            else { return }
            self.client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
            self.client?.urlProtocol(self, didLoad: Self.responseData)
            self.client?.urlProtocolDidFinishLoading(self)
        }
    }

    override func stopLoading() {
        stateLock.lock()
        stopped = true
        stateLock.unlock()
    }

    private func deliverIfNotStopped(_ callback: () -> Void) {
        stateLock.lock()
        defer { stateLock.unlock() }
        guard !stopped else { return }
        callback()
    }

    private static func locked<T>(_ body: () -> T) -> T {
        lock.lock()
        defer { lock.unlock() }
        return body()
    }
}
