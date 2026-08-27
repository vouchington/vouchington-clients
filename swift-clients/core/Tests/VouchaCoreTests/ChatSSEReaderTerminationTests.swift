@testable import VouchaAPI
@testable import VouchaModels
import XCTest

final class ChatSSEReaderTerminationTests: XCTestCase {
    func testThrowsIncompleteStreamWhenEOFArrivesBeforeTerminalEvent() async throws {
        let lines = AsyncStream<String> { continuation in
            continuation.yield("event: text")
            continuation.yield(#"data: {"content":"partial"}"#)
            continuation.yield("")
            continuation.finish()
        }
        do {
            _ = try await collect(ChatSSEReader.events(from: lines))
            XCTFail("Expected incomplete stream")
        } catch {
            XCTAssertEqual(error as? ChatSSEReaderError, .incompleteStream)
        }
    }

    func testNamedErrorIsTerminal() async throws {
        let lines = AsyncStream<String> { continuation in
            continuation.yield("event: error")
            continuation.yield(#"data: {"error":"failed"}"#)
            continuation.yield("")
            continuation.finish()
        }
        let events = try await collect(ChatSSEReader.events(from: lines))
        XCTAssertEqual(events.count, 1)
        guard case let .error(message) = events[0] else {
            return XCTFail("Expected terminal error event")
        }
        XCTAssertEqual(message, "failed")
    }

    private func collect(
        _ stream: AsyncThrowingStream<ChatStreamEvent, Error>
    ) async throws -> [ChatStreamEvent] {
        var events: [ChatStreamEvent] = []
        for try await event in stream {
            events.append(event)
        }
        return events
    }
}
