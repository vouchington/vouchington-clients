import Foundation
@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaModels
import XCTest

final class ChatSSEReaderTests: XCTestCase {
    override func setUp() {
        super.setUp()
        EventStreamURLProtocol.handlers = [:]
        EventStreamURLProtocol.capturedBodies = []
        EventStreamURLProtocol.capturedHeaders = []
    }

    override func tearDown() {
        EventStreamURLProtocol.handlers = [:]
        EventStreamURLProtocol.capturedBodies = []
        EventStreamURLProtocol.capturedHeaders = []
        super.tearDown()
    }

    func testParserHandlesCommentsCRLFMultilineAndFlush() throws {
        var parser = ChatSSEParser()

        XCTAssertTrue(try parser.processLine(": comment").isEmpty)
        XCTAssertTrue(try parser.processLine("event: text\r").isEmpty)
        XCTAssertTrue(try parser.processLine("data: hello").isEmpty)
        XCTAssertTrue(try parser.processLine("data: world").isEmpty)

        let frames = try parser.flush()
        XCTAssertEqual(frames, [ChatSSEFrame(eventType: "text", rawData: "hello\nworld")])
    }

    func testParserProcessesChunksAndEmptyEventDefaultsToMessage() throws {
        var parser = ChatSSEParser()

        XCTAssertTrue(try parser.processChunk("event").isEmpty)
        XCTAssertTrue(try parser.processChunk(":\n").isEmpty)
        XCTAssertTrue(try parser.processChunk("data: first").isEmpty)

        let frames = try parser.processChunk("\n\n")

        XCTAssertEqual(frames, [ChatSSEFrame(eventType: "message", rawData: "first")])
        XCTAssertTrue(try parser.flush().isEmpty)
    }

    func testParserRejectsUnterminatedOversizedFrame() {
        var parser = ChatSSEParser()

        XCTAssertThrowsError(try parser.processChunk(String(
            repeating: "x",
            count: ChatSSEParser.maximumFrameCharacters + 1
        ))) {
            XCTAssertEqual($0 as? ChatSSEParserError, .frameTooLarge)
        }
    }

    func testParserRejectsOversizedEventField() {
        var parser = ChatSSEParser()
        let line = "event: " + String(
            repeating: "x",
            count: ChatSSEParser.maximumFrameCharacters + 1
        )

        XCTAssertThrowsError(try parser.processLine(line)) {
            XCTAssertEqual($0 as? ChatSSEParserError, .frameTooLarge)
        }
    }

    func testBoundedResponseLinesRejectOversizedSSELineKindsBeforeMaterializingStrings() async {
        for prefix in ["data: ", ": ", "unknown: "] {
            let bytes = AsyncStream<UInt8>(bufferingPolicy: .unbounded) { continuation in
                for byte in Data((prefix + String(
                    repeating: "x",
                    count: ChatSSEParser.maximumFrameCharacters
                )).utf8) {
                    continuation.yield(byte)
                }
                continuation.finish()
            }

            do {
                _ = try await collectLines(BoundedResponseLineReader.lines(from: bytes))
                XCTFail("Expected \(prefix) line to be rejected")
            } catch {
                XCTAssertEqual(error as? ChatSSEParserError, .frameTooLarge)
            }
        }
    }

    func testBoundedResponseLinesNormalizeCRLFBeforeSSEParsing() async throws {
        let bytes = AsyncStream<UInt8>(bufferingPolicy: .unbounded) { continuation in
            for byte in Data("event: text\r\ndata: {\"content\":\"Hi\"}\r\n\r\n".utf8) {
                continuation.yield(byte)
            }
            continuation.finish()
        }

        let lines = try await collectLines(BoundedResponseLineReader.lines(from: bytes))

        XCTAssertEqual(lines, ["event: text", #"data: {"content":"Hi"}"#, ""])
    }

    func testReaderMapsEventTypesAndDefaultMessageEvents() async throws {
        let lines = AsyncStream<String> { continuation in
            continuation.yield("event: metadata")
            continuation.yield(
                #"data: {"conversation_id":"conv-1","user_message_id":"user-1","assistant_message_id":"assistant-1","job_id":"job-1"}"#
            )
            continuation.yield("")
            continuation.yield("event: text")
            continuation.yield(#"data: {"content":"streamed "}"#)
            continuation.yield("")
            continuation.yield(#"data: {"content":"streamed text"}"#)
            continuation.yield("")
            continuation.yield("event: tool_call")
            continuation.yield(#"data: {"tool_call_id":"tool-1","name":"search","arguments":"{}"}"#)
            continuation.yield("")
            continuation.yield("event: tool_result")
            continuation.yield(#"data: {"tool_call_id":"tool-1","result":{"status":"ok"}}"#)
            continuation.yield("")
            continuation.yield("event: subagent_step")
            continuation.yield(#"data: {"agent_name":"helper","tool_name":"search","tool_call_id":"tool-1"}"#)
            continuation.yield("")
            continuation.yield("event: subagent_text")
            continuation.yield(#"data: {"agent_name":"helper","tool_call_id":"tool-1","content":"Working"}"#)
            continuation.yield("")
            continuation.yield("event: error")
            continuation.yield(#"data: {"error":"boom"}"#)
            continuation.yield("")
            continuation.yield("event: done")
            continuation.yield("data: {}")
            continuation.yield("")
            continuation.finish()
        }

        let events = try await collect(ChatSSEReader.events(from: lines))

        XCTAssertEqual(events.count, 8)
        guard case let .metadata(metadata) = events[0] else {
            return XCTFail("Expected metadata event")
        }
        XCTAssertEqual(metadata.conversationId, "conv-1")
        XCTAssertEqual(metadata.userMessageId, "user-1")
        XCTAssertEqual(metadata.assistantMessageId, "assistant-1")
        XCTAssertEqual(metadata.jobId, "job-1")

        guard case let .text(chunk) = events[1] else {
            return XCTFail("Expected text event")
        }
        XCTAssertEqual(chunk, "streamed ")

        guard case let .message(content) = events[2] else {
            return XCTFail("Expected default message event")
        }
        XCTAssertEqual(content, "streamed text")

        guard case let .toolCall(toolCall) = events[3] else {
            return XCTFail("Expected tool call event")
        }
        XCTAssertEqual(toolCall.toolCallId, "tool-1")
        XCTAssertEqual(toolCall.name, "search")
        XCTAssertEqual(toolCall.arguments, "{}")

        guard case let .toolResult(toolCallId, result) = events[4] else {
            return XCTFail("Expected tool result event")
        }
        XCTAssertEqual(toolCallId, "tool-1")
        switch result {
        case let .object(object):
            guard case .string("ok") = object["status"] else {
                return XCTFail("Expected tool result payload")
            }
        default:
            XCTFail("Expected tool result payload")
        }

        guard case let .subagentStep(step) = events[5] else {
            return XCTFail("Expected subagent step event")
        }
        XCTAssertEqual(step.agentName, "helper")
        XCTAssertEqual(step.toolName, "search")
        XCTAssertEqual(step.toolCallId, "tool-1")

        guard case let .subagentText(chunk) = events[6] else {
            return XCTFail("Expected subagent text event")
        }
        XCTAssertEqual(chunk.agentName, "helper")
        XCTAssertEqual(chunk.toolCallId, "tool-1")
        XCTAssertEqual(chunk.content, "Working")

        guard case let .error(message) = events[7] else {
            return XCTFail("Expected error event")
        }
        XCTAssertEqual(message, "boom")

    }

    func testReaderSkipsMalformedEventsAndPropagatesLineErrors() async {
        let lines = AsyncThrowingStream<String, Error> { continuation in
            continuation.yield("event: text")
            continuation.yield("data: not-json")
            continuation.yield("")
            continuation.yield("event: subagent_step")
            continuation.yield(#"data: {"agent_name":"helper"}"#)
            continuation.yield("")
            continuation.finish(throwing: URLError(.cannotParseResponse))
        }

        do {
            _ = try await collect(ChatSSEReader.events(from: lines))
            XCTFail("Expected stream error")
        } catch {
            XCTAssertEqual((error as? URLError)?.code, .cannotParseResponse)
        }
    }

    func testChatMessageContentDecodesStringContent() throws {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        let message = try decoder.decode(
            ChatMessage.self,
            from: Data(
                #"{"id":"message-1","conversation_id":"conversation-1","created_at":"2026-01-01T00:00:00Z","created_by_id":"user-1","updated_at":"2026-01-01T00:00:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null,"content":"plain text"}"#
                    .utf8
            )
        )

        XCTAssertEqual(message.content.role, "message")
        XCTAssertEqual(message.content.content, "plain text")
        XCTAssertEqual(message.content.displayText, "plain text")
        XCTAssertNil(message.content.error)
    }

    func testValidateContentTypeRejectsNonEventStreamResponses() throws {
        let url = try XCTUnwrap(URL(string: "http://localhost/chat"))
        let response = try XCTUnwrap(HTTPURLResponse(
            url: url,
            statusCode: 200,
            httpVersion: "HTTP/1.1",
            headerFields: ["Content-Type": "application/json"]
        ))

        XCTAssertThrowsError(try ChatSSEReader.validateContentType(response))
    }

    func testValidateContentTypeAcceptsEventStreamResponses() throws {
        let url = try XCTUnwrap(URL(string: "http://localhost/chat"))
        let response = try XCTUnwrap(HTTPURLResponse(
            url: url,
            statusCode: 200,
            httpVersion: "HTTP/1.1",
            headerFields: ["Content-Type": "text/event-stream; charset=utf-8"]
        ))

        XCTAssertNoThrow(try ChatSSEReader.validateContentType(response))
    }

    func testValidateContentTypeRejectsNonHttpResponses() throws {
        let response = try URLResponse(
            url: XCTUnwrap(URL(string: "http://localhost/chat")),
            mimeType: "text/event-stream",
            expectedContentLength: -1,
            textEncodingName: nil
        )

        XCTAssertThrowsError(try ChatSSEReader.validateContentType(response))
    }

    func testApiClientStreamsChatConversationEvents() async throws {
        EventStreamURLProtocol.handlers["/api/v1/conversations/conversation-1/chat"] = (
            Data("event: text\ndata: {\"content\":\"Hi\"}\n\nevent: done\ndata: {}\n\n".utf8),
            200,
            "text/event-stream"
        )
        let client = try APIClient(
            config: AppConfig(
                baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
                turnstileSiteKey: "test-site-key"
            ),
            cookieStorage: IsolatedHTTPCookieStorage.make(),
            protocolClasses: [EventStreamURLProtocol.self],
            metadata: ClientMetadata(platform: .ios, appVersion: "2.3.4", sdkVersion: "1.2.0")
        )

        let stream = await client.streamChatConversation(conversationId: "conversation-1", message: "Hello")
        let events = try await collect(stream)

        XCTAssertEqual(EventStreamURLProtocol.capturedBodies.first??.contains(#""message":"Hello""#), true)
        XCTAssertEqual(EventStreamURLProtocol.capturedHeaders.first?["x-voucha-client"], "swift")
        XCTAssertEqual(EventStreamURLProtocol.capturedHeaders.first?["x-voucha-platform"], "ios")
        XCTAssertEqual(EventStreamURLProtocol.capturedHeaders.first?["x-voucha-app-version"], "2.3.4")
        XCTAssertEqual(EventStreamURLProtocol.capturedHeaders.first?["x-voucha-sdk-version"], "1.2.0")
        XCTAssertEqual(events.count, 1)
    }

    func testApiClientStreamSurfacesResponseErrors() async throws {
        EventStreamURLProtocol.handlers["/api/v1/conversations/conversation-1/chat"] = (
            Data(#"{"message":"Nope"}"#.utf8),
            400,
            "application/json"
        )
        let client = try APIClient(
            config: AppConfig(
                baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
                turnstileSiteKey: "test-site-key"
            ),
            cookieStorage: IsolatedHTTPCookieStorage.make(),
            protocolClasses: [EventStreamURLProtocol.self]
        )

        do {
            _ = try await collect(client.streamChatConversation(conversationId: "conversation-1", message: "Hello"))
            XCTFail("Expected stream error")
        } catch let error as VouchaError {
            XCTAssertEqual(error.errorDescription, "Nope")
        } catch {
            XCTFail("Expected VouchaError, got \(error)")
        }
    }

    func testApiClientStreamDecodesLineDelimitedJSON() async throws {
        EventStreamURLProtocol.handlers["/api/v1/streaming-items"] = (
            Data("{\"id\":\"first\"}\n\nnot-json\n{\"id\":\"second\"}\n".utf8),
            200,
            "application/x-ndjson"
        )
        let client = try APIClient(
            config: AppConfig(
                baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
                turnstileSiteKey: "test-site-key"
            ),
            cookieStorage: IsolatedHTTPCookieStorage.make(),
            protocolClasses: [EventStreamURLProtocol.self]
        )

        let items = try await collect(client.stream(Endpoint(path: "/api/v1/streaming-items")) as AsyncThrowingStream<
            StreamingItem,
            Error
        >)

        XCTAssertEqual(items.map(\.id), ["first", "second"])
    }

    func testReaderCancelsCleanly() async throws {
        let lines = AsyncStream<String> { continuation in
            continuation.yield("data: hello")
        }
        let stream = ChatSSEReader.events(from: lines)
        let task = Task<[ChatStreamEvent], Never> {
            var events: [ChatStreamEvent] = []
            do {
                for try await event in stream {
                    events.append(event)
                }
            } catch {
                return events
            }
            return events
        }

        try await Task.sleep(nanoseconds: 50_000_000)
        task.cancel()
        let events = await task.value
        XCTAssertTrue(events.isEmpty)
    }

    private func collect(_ stream: AsyncThrowingStream<ChatStreamEvent, Error>) async throws -> [ChatStreamEvent] {
        var events: [ChatStreamEvent] = []
        for try await event in stream {
            events.append(event)
        }
        return events
    }

    private func collect(_ stream: AsyncThrowingStream<StreamingItem, Error>) async throws -> [StreamingItem] {
        var items: [StreamingItem] = []
        for try await item in stream {
            items.append(item)
        }
        return items
    }

    private func collectLines(_ stream: AsyncThrowingStream<String, Error>) async throws -> [String] {
        var lines: [String] = []
        for try await line in stream {
            lines.append(line)
        }
        return lines
    }
}

private struct StreamingItem: Decodable {
    let id: String
}

private final class EventStreamURLProtocol: URLProtocol {
    static var handlers: [String: (Data, Int, String)] = [:]
    static var capturedBodies: [String?] = []
    static var capturedHeaders: [[String: String]] = []
    private static let lock = NSLock()

    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        let path = request.url?.path ?? ""
        let responseInfo = Self.response(for: request, path: path)
        let response = HTTPURLResponse(
            url: request.url!,
            statusCode: responseInfo.status,
            httpVersion: "HTTP/1.1",
            headerFields: ["Content-Type": responseInfo.contentType]
        )!
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: responseInfo.data)
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {}

    private static func response(
        for request: URLRequest,
        path: String
    ) -> (data: Data, status: Int, contentType: String) {
        lock.lock()
        defer { lock.unlock() }
        capturedBodies.append(capturedBody(from: request))
        capturedHeaders.append(request.allHTTPHeaderFields ?? [:])
        return handlers[path] ?? (Data("{}".utf8), 200, "text/event-stream")
    }

    private static func capturedBody(from request: URLRequest) -> String? {
        if let body = request.httpBody {
            return String(data: body, encoding: .utf8)
        }
        guard let stream = request.httpBodyStream else { return nil }
        stream.open()
        defer { stream.close() }
        var data = Data()
        let bufferSize = 1_024
        let buffer = UnsafeMutablePointer<UInt8>.allocate(capacity: bufferSize)
        defer { buffer.deallocate() }
        while stream.hasBytesAvailable {
            let read = stream.read(buffer, maxLength: bufferSize)
            if read <= 0 {
                break
            }
            data.append(buffer, count: read)
        }
        return String(data: data, encoding: .utf8)
    }
}
