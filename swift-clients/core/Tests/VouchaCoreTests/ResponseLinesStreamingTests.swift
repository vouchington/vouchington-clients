import Foundation
@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaModels
import XCTest

final class ResponseLinesStreamingTests: XCTestCase {
    override func setUp() {
        super.setUp()
        DelayedStreamURLProtocol.reset()
    }

    override func tearDown() {
        DelayedStreamURLProtocol.reset()
        super.tearDown()
    }

    func testDelegateTransportYieldsNDJSONBeforeResponseCompletes() async throws {
        DelayedStreamURLProtocol.handlers["/api/v1/streaming-items"] = DelayedStreamResponse(
            chunks: [
                Data(#"{"id":"first"}"#.utf8) + Data("\n".utf8),
                Data(#"{"id":"second"}"#.utf8) + Data("\n".utf8)
            ],
            status: 200,
            contentType: "application/x-ndjson",
            delayNanoseconds: 200_000_000
        )
        let client = try makeClient()
        let request = try URLRequest(url: XCTUnwrap(URL(string: "http://localhost:2999/api/v1/streaming-items")))

        let lineResponse = try await client.responseLinesUsingURLSessionDataDelegate(for: request)
        var iterator = lineResponse.lines.makeAsyncIterator()

        let firstLine = try await awaitValue { try await iterator.next() }

        XCTAssertEqual(firstLine, #"{"id":"first"}"#)
        XCTAssertEqual(DelayedStreamURLProtocol.deliveredChunkCount(for: "/api/v1/streaming-items"), 1)
        XCTAssertFalse(DelayedStreamURLProtocol.didFinish(path: "/api/v1/streaming-items"))

        let secondLine = try await awaitValue { try await iterator.next() }
        XCTAssertEqual(secondLine, #"{"id":"second"}"#)
        _ = try await awaitValue { try await iterator.next() }
        XCTAssertTrue(DelayedStreamURLProtocol.didFinish(path: "/api/v1/streaming-items"))
    }

    func testDelegateTransportYieldsSSEBeforeResponseCompletes() async throws {
        DelayedStreamURLProtocol.handlers["/api/v1/conversations/conversation-1/chat"] = DelayedStreamResponse(
            chunks: [
                Data("event: text\ndata: {\"content\":\"Hi\"}\n\n".utf8),
                Data("event: done\ndata: {}\n\n".utf8)
            ],
            status: 200,
            contentType: "text/event-stream",
            delayNanoseconds: 200_000_000
        )
        let client = try makeClient()
        let request = try URLRequest(
            url: XCTUnwrap(URL(string: "http://localhost:2999/api/v1/conversations/conversation-1/chat"))
        )
        let lineResponse = try await client.responseLinesUsingURLSessionDataDelegate(for: request)
        var iterator = ChatSSEReader.events(from: lineResponse.lines).makeAsyncIterator()

        let firstEvent = try await awaitValue { try await iterator.next() }

        guard case let .text(content)? = firstEvent else {
            return XCTFail("Expected first SSE text event")
        }
        XCTAssertEqual(content, "Hi")
        XCTAssertEqual(
            DelayedStreamURLProtocol.deliveredChunkCount(for: "/api/v1/conversations/conversation-1/chat"),
            1
        )
        XCTAssertFalse(DelayedStreamURLProtocol.didFinish(path: "/api/v1/conversations/conversation-1/chat"))

        let secondEvent = try await awaitValue { try await iterator.next() }
        guard case .done? = secondEvent else {
            return XCTFail("Expected final done event")
        }
    }

    func testDelegateTransportPreservesErrorResponseBody() async throws {
        DelayedStreamURLProtocol.handlers["/api/v1/conversations/conversation-1/chat"] = DelayedStreamResponse(
            chunks: [Data(#"{"message":"Nope"}"#.utf8)],
            status: 400,
            contentType: "application/json",
            delayNanoseconds: 0
        )
        let client = try makeClient()
        let request = try URLRequest(
            url: XCTUnwrap(URL(string: "http://localhost:2999/api/v1/conversations/conversation-1/chat"))
        )

        let lineResponse = try await client.responseLinesUsingURLSessionDataDelegate(for: request)

        XCTAssertEqual(String(data: lineResponse.body, encoding: .utf8), #"{"message":"Nope"}"#)
        var iterator = lineResponse.lines.makeAsyncIterator()
        let errorLine = try await awaitValue { try await iterator.next() }
        XCTAssertNil(errorLine)
        do {
            try await client.validate(response: lineResponse.response, data: lineResponse.body)
            XCTFail("Expected validation error")
        } catch {
            XCTAssertEqual((error as? VouchaError)?.errorDescription, "Nope")
        }
    }

    func testDelegateTransportCapsErrorDiagnosticsAndCancelsTheRequest() async throws {
        let path = "/api/v1/oversized-error"
        DelayedStreamURLProtocol.handlers[path] = DelayedStreamResponse(
            chunks: [
                Data(repeating: 0x61, count: ResponseBodyLimit.maximumDiagnosticBytes),
                Data([0x62])
            ],
            status: 500,
            contentType: "application/json",
            delayNanoseconds: 20_000_000
        )
        let client = try makeClient()
        let request = try URLRequest(url: XCTUnwrap(URL(string: "http://localhost:2999\(path)")))

        do {
            _ = try await client.responseLinesUsingURLSessionDataDelegate(for: request)
            XCTFail("Expected oversized error diagnostic to fail")
        } catch {
            XCTAssertEqual(error as? ResponseLineBufferError, .bodyTooLarge)
        }
        let didCancel = try await awaitValue {
            while !DelayedStreamURLProtocol.didCancel(path: path) {
                try await Task.sleep(nanoseconds: 10_000_000)
            }
            return true
        }
        XCTAssertTrue(didCancel)
    }

    func testDelegateTransportFlushesFinalLineWithoutTrailingNewline() async throws {
        DelayedStreamURLProtocol.handlers["/api/v1/final-line"] = DelayedStreamResponse(
            chunks: [Data(#"{"id":"final"}"#.utf8)],
            status: 200,
            contentType: "application/x-ndjson",
            delayNanoseconds: 0
        )
        let client = try makeClient()
        let request = try URLRequest(url: XCTUnwrap(URL(string: "http://localhost:2999/api/v1/final-line")))

        let lineResponse = try await client.responseLinesUsingURLSessionDataDelegate(for: request)
        var iterator = lineResponse.lines.makeAsyncIterator()

        let line = try await awaitValue { try await iterator.next() }
        XCTAssertEqual(line, #"{"id":"final"}"#)
        let finalLine = try await awaitValue { try await iterator.next() }
        XCTAssertNil(finalLine)
    }

    func testDelegateTransportAcceptsManyCompleteLinesInOneChunk() async throws {
        let lineCount = 350_000
        DelayedStreamURLProtocol.handlers["/api/v1/many-lines"] = DelayedStreamResponse(
            chunks: [Data(String(repeating: "{}\n", count: lineCount).utf8)],
            status: 200,
            contentType: "application/x-ndjson",
            delayNanoseconds: 0
        )
        let client = try makeClient()
        let request = try URLRequest(url: XCTUnwrap(URL(string: "http://localhost:2999/api/v1/many-lines")))
        let lineResponse = try await client.responseLinesUsingURLSessionDataDelegate(for: request)
        var received = 0
        for try await _ in lineResponse.lines {
            received += 1
        }

        XCTAssertEqual(received, lineCount)
    }

    func testDelegateTransportRejectsAnUnterminatedOversizedLine() async throws {
        DelayedStreamURLProtocol.handlers["/api/v1/oversized-line"] = DelayedStreamResponse(
            chunks: [Data(repeating: 0x61, count: ResponseLineBuffer.maximumLineBytes + 1)],
            status: 200,
            contentType: "application/x-ndjson",
            delayNanoseconds: 0
        )
        let client = try makeClient()
        let request = try URLRequest(url: XCTUnwrap(URL(string: "http://localhost:2999/api/v1/oversized-line")))
        let lineResponse = try await client.responseLinesUsingURLSessionDataDelegate(for: request)

        do {
            for try await _ in lineResponse.lines {}
            XCTFail("Expected oversized line to fail")
        } catch {
            XCTAssertEqual(error as? ResponseLineBufferError, .lineTooLarge)
        }
    }

    func testDelegateTransportUsesDefaultFixtureForUnregisteredPath() async throws {
        let client = try makeClient()
        let request = try URLRequest(url: XCTUnwrap(URL(string: "http://localhost:2999/api/v1/default")))

        let lineResponse = try await client.responseLinesUsingURLSessionDataDelegate(for: request)
        var iterator = lineResponse.lines.makeAsyncIterator()

        let line = try await awaitValue { try await iterator.next() }
        XCTAssertEqual(line, "{}")
        let finalLine = try await awaitValue { try await iterator.next() }
        XCTAssertNil(finalLine)
    }

    func testDelegateTransportMapsBufferedCompletionError() async throws {
        DelayedStreamURLProtocol.handlers["/api/v1/error-after-response"] = DelayedStreamResponse(
            chunks: [Data(#"{"message":"late failure"}"#.utf8)],
            status: 500,
            contentType: "application/json",
            delayNanoseconds: 0,
            completionError: URLError(.timedOut)
        )
        let client = try makeClient()
        let request = try URLRequest(url: XCTUnwrap(URL(string: "http://localhost:2999/api/v1/error-after-response")))

        do {
            _ = try await client.responseLinesUsingURLSessionDataDelegate(for: request)
            XCTFail("Expected buffered completion error")
        } catch let VouchaError.network(error) {
            XCTAssertEqual(error.code, .timedOut)
        } catch {
            XCTFail("Expected network error, received \(error)")
        }
    }

    func testDelegateTransportMapsCompletionBeforeResponse() async throws {
        DelayedStreamURLProtocol.handlers["/api/v1/no-response"] = DelayedStreamResponse(
            chunks: [],
            status: 200,
            contentType: "application/json",
            delayNanoseconds: 0,
            completionError: URLError(.notConnectedToInternet),
            sendsResponse: false
        )
        let client = try makeClient()
        let request = try URLRequest(url: XCTUnwrap(URL(string: "http://localhost:2999/api/v1/no-response")))

        do {
            _ = try await client.responseLinesUsingURLSessionDataDelegate(for: request)
            XCTFail("Expected completion-before-response error")
        } catch let VouchaError.network(error) {
            XCTAssertEqual(error.code, .notConnectedToInternet)
        } catch {
            XCTFail("Expected network error, received \(error)")
        }
    }

    func testDelegateTransportCancelsBeforeResponseArrives() async throws {
        DelayedStreamURLProtocol.handlers["/api/v1/slow-response"] = DelayedStreamResponse(
            chunks: [Data(#"{"id":"late"}"#.utf8)],
            status: 200,
            contentType: "application/x-ndjson",
            delayNanoseconds: 0,
            responseDelayNanoseconds: 1_000_000_000
        )
        let client = try makeClient()
        let request = try URLRequest(url: XCTUnwrap(URL(string: "http://localhost:2999/api/v1/slow-response")))

        let task = Task {
            try await client.responseLinesUsingURLSessionDataDelegate(for: request)
        }
        try await Task.sleep(nanoseconds: 20_000_000)
        task.cancel()

        do {
            _ = try await awaitValue { try await task.value }
            XCTFail("Expected cancellation before response")
        } catch is CancellationError {
            let didCancel = try await awaitValue {
                while !DelayedStreamURLProtocol.didCancel(path: "/api/v1/slow-response") {
                    try await Task.sleep(nanoseconds: 10_000_000)
                }
                return true
            }
            XCTAssertTrue(didCancel)
        }
    }

    func testResponseLinesTransportHelpers() async throws {
        let transport = ResponseLinesTransport()
        let response = try URLResponse(
            url: XCTUnwrap(URL(string: "https://example.com/plain")),
            mimeType: nil,
            expectedContentLength: 0,
            textEncodingName: nil
        )
        XCTAssertTrue(transport.isSuccessful(response: response))
        XCTAssertTrue(transport.mapError(URLError(.cancelled)) is CancellationError)

        let mappedError = transport.mapError(URLError(.timedOut))
        guard case let VouchaError.network(networkError) = mappedError else {
            return XCTFail("Expected network error")
        }
        XCTAssertEqual(networkError.code, .timedOut)

        var iterator = transport.makeLineStream(from: Data("one\ntwo".utf8)).makeAsyncIterator()
        let firstLine = try await awaitValue { try await iterator.next() }
        XCTAssertEqual(firstLine, "one")
        let secondLine = try await awaitValue { try await iterator.next() }
        XCTAssertEqual(secondLine, "two")
        let finalLine = try await awaitValue { try await iterator.next() }
        XCTAssertNil(finalLine)
    }

    private func makeClient() throws -> APIClient {
        try APIClient(
            config: AppConfig(
                baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
                turnstileSiteKey: "test-site-key"
            ),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [DelayedStreamURLProtocol.self]
        )
    }

    private func awaitValue<T>(_ operation: @escaping () async throws -> T) async throws -> T {
        try await withThrowingTaskGroup(of: T.self) { group in
            group.addTask { try await operation() }
            group.addTask {
                try await Task.sleep(nanoseconds: 3_000_000_000)
                throw ResponseLinesStreamingTimeout.timedOut
            }
            let nextValue = try await group.next()
            let value = try XCTUnwrap(nextValue)
            group.cancelAll()
            return value
        }
    }
}

private enum ResponseLinesStreamingTimeout: Error {
    case timedOut
}

private struct DelayedStreamResponse {
    let chunks: [Data]
    let status: Int
    let contentType: String
    let delayNanoseconds: UInt64
    let responseDelayNanoseconds: UInt64
    let completionError: Error?
    let sendsResponse: Bool

    init(
        chunks: [Data],
        status: Int,
        contentType: String,
        delayNanoseconds: UInt64,
        responseDelayNanoseconds: UInt64 = 0,
        completionError: Error? = nil,
        sendsResponse: Bool = true
    ) {
        self.chunks = chunks
        self.status = status
        self.contentType = contentType
        self.delayNanoseconds = delayNanoseconds
        self.responseDelayNanoseconds = responseDelayNanoseconds
        self.completionError = completionError
        self.sendsResponse = sendsResponse
    }
}

private final class DelayedStreamURLProtocol: URLProtocol {
    static var handlers: [String: DelayedStreamResponse] = [:]
    private static var deliveredChunks: [String: Int] = [:]
    private static var finishedPaths: Set<String> = []
    private static var cancelledPaths: Set<String> = []
    private static let lock = NSLock()
    private var sendTask: Task<Void, Never>?

    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        let path = request.url?.path ?? ""
        let response = Self.response(for: path)
        if response.responseDelayNanoseconds == 0 {
            receive(response: response)
        }
        sendTask = Task { [weak self] in
            if response.responseDelayNanoseconds > 0 {
                try? await Task.sleep(nanoseconds: response.responseDelayNanoseconds)
                guard !Task.isCancelled else { return }
                self?.receive(response: response)
            }
            await self?.send(response: response, path: path)
        }
    }

    override func stopLoading() {
        Self.recordCancel(path: request.url?.path ?? "")
        sendTask?.cancel()
    }

    private func receive(response: DelayedStreamResponse) {
        guard response.sendsResponse else { return }
        guard let url = request.url else {
            client?.urlProtocol(self, didFailWithError: URLError(.badURL))
            return
        }
        let urlResponse = HTTPURLResponse(
            url: url,
            statusCode: response.status,
            httpVersion: "HTTP/1.1",
            headerFields: ["Content-Type": response.contentType]
        )!
        client?.urlProtocol(self, didReceive: urlResponse, cacheStoragePolicy: .notAllowed)
    }

    private func send(response: DelayedStreamResponse, path: String) async {
        for chunk in response.chunks {
            guard !Task.isCancelled else { return }
            client?.urlProtocol(self, didLoad: chunk)
            Self.recordChunk(path: path)
            guard response.delayNanoseconds > 0 else { continue }
            try? await Task.sleep(nanoseconds: response.delayNanoseconds)
        }
        guard !Task.isCancelled else { return }
        if let completionError = response.completionError {
            client?.urlProtocol(self, didFailWithError: completionError)
            return
        }
        Self.recordFinish(path: path)
        client?.urlProtocolDidFinishLoading(self)
    }

    static func reset() {
        lock.lock()
        handlers = [:]
        deliveredChunks = [:]
        finishedPaths = []
        cancelledPaths = []
        lock.unlock()
    }

    static func deliveredChunkCount(for path: String) -> Int {
        lock.lock()
        defer { lock.unlock() }
        return deliveredChunks[path] ?? 0
    }

    static func didFinish(path: String) -> Bool {
        lock.lock()
        defer { lock.unlock() }
        return finishedPaths.contains(path)
    }

    static func didCancel(path: String) -> Bool {
        lock.lock()
        defer { lock.unlock() }
        return cancelledPaths.contains(path)
    }

    private static func response(for path: String) -> DelayedStreamResponse {
        lock.lock()
        defer { lock.unlock() }
        return handlers[path] ?? DelayedStreamResponse(
            chunks: [Data("{}".utf8)],
            status: 200,
            contentType: "application/json",
            delayNanoseconds: 0
        )
    }

    private static func recordChunk(path: String) {
        lock.lock()
        deliveredChunks[path, default: 0] += 1
        lock.unlock()
    }

    private static func recordFinish(path: String) {
        lock.lock()
        finishedPaths.insert(path)
        lock.unlock()
    }

    private static func recordCancel(path: String) {
        lock.lock()
        cancelledPaths.insert(path)
        lock.unlock()
    }
}
