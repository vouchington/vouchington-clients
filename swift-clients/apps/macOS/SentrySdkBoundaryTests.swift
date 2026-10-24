import Sentry
import VouchaCore
import XCTest

final class SentrySdkBoundaryTests: XCTestCase {
    func testMcpSessionIdIsRedactedFromTheSdkEvent() throws {
        let callbackEvent = capturedEvent()
        let breadcrumb = try XCTUnwrap(callbackEvent.breadcrumbs?.first)
        let fromCallback = try XCTUnwrap(SentryEventProjection.scrubbedBreadcrumb(breadcrumb))
        // `scrubbedBreadcrumb` mutates in place, so the `apply` path needs its own unscrubbed event.
        let event = capturedEvent()
        XCTAssertEqual(event.breadcrumbs?.first?.data?["Mcp-Session-Id"] as? String, sessionId)
        let draft = SentryEventProjection.draft(from: event, boundAccountId: nil)
        SentryEventProjection.apply(SentryEventScrubber.scrub(draft), to: event)

        XCTAssertEqual(event.request?.headers?["Mcp-Session-Id"], SentryEventScrubber.filteredValue)
        XCTAssertEqual(event.request?.headers?["MCP-SESSION-ID"], SentryEventScrubber.filteredValue)
        XCTAssertEqual(event.request?.headers?["Accept"], "application/json")
        XCTAssertEqual(fromCallback.data?["url"] as? String, "https://voucha.ai/mcp")
        XCTAssertNotEqual(event.request?.headers?["Mcp-Session-Id"], sessionId)
        XCTAssertNotEqual(event.request?.headers?["MCP-SESSION-ID"], sessionId)
        XCTAssertNil(fromCallback.data?["Mcp-Session-Id"])
        XCTAssertFalse(fromCallback.data?.values.contains { ($0 as? String) == sessionId } == true)
        XCTAssertFalse(event.breadcrumbs?.first?.data?.values.contains { ($0 as? String) == sessionId } == true)
    }

    private func capturedEvent() -> Event {
        let event = Event()
        let request = SentryRequest()
        request.url = "https://voucha.ai/mcp"
        request.headers = [
            "Mcp-Session-Id": sessionId,
            "MCP-SESSION-ID": "other-session-secret",
            "Accept": "application/json"
        ]
        event.request = request
        let breadcrumb = Breadcrumb(level: .info, category: "http")
        breadcrumb.message = "POST /mcp"
        breadcrumb.setData(value: sessionId, key: "Mcp-Session-Id")
        breadcrumb.setData(value: "https://voucha.ai/mcp", key: "url")
        event.breadcrumbs = [breadcrumb]
        return event
    }
}

private let sessionId = "mcp-session-secret"
