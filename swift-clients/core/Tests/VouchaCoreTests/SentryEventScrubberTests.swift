@testable import VouchaCore
import XCTest

final class SentryEventScrubberTests: XCTestCase {
    func testMissingDsnDoesNotCallTheTransport() {
        let transport = RecordingSentryTransport()
        let runtime = SentryRuntime.open(
            configuration: .release,
            releaseName: "0.1.0+1",
            accountId: "account-1",
            transport: transport
        )
        XCTAssertNil(runtime)
        XCTAssertTrue(transport.events.isEmpty)
    }

    func testFakeDsnSendsOneScrubbedEventThroughTheStubTransport() {
        let transport = RecordingSentryTransport()
        let runtime = SentryRuntime.open(
            environment: ["VOUCHA_SENTRY_DSN": "https://public@example.invalid/1"],
            configuration: .debug,
            releaseName: "0.1.0+1",
            accountId: "account-1",
            transport: transport
        )
        runtime?.capture(secretEvent)
        XCTAssertEqual(transport.events.count, 1)
        let event = transport.events[0]
        XCTAssertEqual(event.accountId, "account-1")
        XCTAssertNil(event.email)
        XCTAssertNil(event.username)
        XCTAssertNil(event.ipAddress)
        XCTAssertEqual(event.requestURL, "https://voucha.ai/api/v1/session")
        XCTAssertNil(event.queryString)
        XCTAssertEqual(event.headers["Authorization"], SentryEventScrubber.filteredValue)
        XCTAssertEqual(event.headers["X-App-Attest-Assertion"], SentryEventScrubber.filteredValue)
        XCTAssertEqual(event.headers["Accept"], "application/json")
        XCTAssertEqual(event.headers["Referer"], "https://voucha.ai/reset")
        XCTAssertEqual(event.cookies, SentryEventScrubber.filteredValue)
        XCTAssertNil(event.body)
        XCTAssertNil(event.extras["chat"])
        XCTAssertNil(event.extras["email_code"])
        XCTAssertEqual(event.extras["screen"], "settings")
        XCTAssertEqual(event.breadcrumbs.map(\.category), ["http"])
        XCTAssertEqual(event.breadcrumbs[0].data["url"], "https://voucha.ai/callback")
        XCTAssertFalse(transport.events.contains { $0.requestURL?.contains("example.invalid") == true })
    }

    func testFragmentBeforeQueryIsRemovedFromTheRequestURL() {
        let transport = RecordingSentryTransport()
        let runtime = SentryRuntime.open(
            environment: ["VOUCHA_SENTRY_DSN": "https://public@example.invalid/1"],
            configuration: .debug,
            releaseName: "0.1.0+1",
            accountId: nil,
            transport: transport
        )
        runtime?.capture(SentryOutboundEvent(
            requestURL: "https://voucha.ai/callback#access_token=secret?state=x"
        ))
        XCTAssertEqual(transport.events.first?.requestURL, "https://voucha.ai/callback")
        XCTAssertNil(transport.events.first?.queryString)
    }

    func testBreadcrumbQueryAndFragmentFieldsAreDropped() {
        let transport = RecordingSentryTransport()
        let runtime = SentryRuntime.open(
            environment: ["VOUCHA_SENTRY_DSN": "https://public@example.invalid/1"],
            configuration: .debug,
            releaseName: "0.1.0+1",
            accountId: nil,
            transport: transport
        )
        runtime?.capture(SentryOutboundEvent(breadcrumbs: [
            SentryBreadcrumbDraft(
                category: "http",
                data: [
                    "url": "https://voucha.ai/search?q=private-search",
                    "http.query": "q=private-search",
                    "http.fragment": "token=secret"
                ]
            )
        ]))
        let data = transport.events.first?.breadcrumbs.first?.data ?? [:]
        XCTAssertEqual(data["url"], "https://voucha.ai/search")
        XCTAssertNil(data["http.query"])
        XCTAssertNil(data["http.fragment"])
    }

    func testMcpSessionIdIsFilteredFromRequestHeadersAndBreadcrumbs() {
        let scrubbed = SentryEventScrubber.scrub(SentryOutboundEvent(
            headers: [
                "Mcp-Session-Id": "mcp-session-secret",
                "MCP-SESSION-ID": "other-session-secret",
                "Accept": "application/json"
            ],
            breadcrumbs: [
                SentryBreadcrumbDraft(
                    category: "http",
                    data: [
                        "Mcp-Session-Id": "mcp-session-secret",
                        "url": "https://voucha.ai/mcp"
                    ]
                )
            ]
        ))
        XCTAssertEqual(scrubbed.headers["Mcp-Session-Id"], SentryEventScrubber.filteredValue)
        XCTAssertEqual(scrubbed.headers["MCP-SESSION-ID"], SentryEventScrubber.filteredValue)
        XCTAssertEqual(scrubbed.headers["Accept"], "application/json")
        let data = scrubbed.breadcrumbs.first?.data ?? [:]
        XCTAssertNil(data["Mcp-Session-Id"])
        XCTAssertEqual(data["url"], "https://voucha.ai/mcp")
        XCTAssertFalse(data.values.contains("mcp-session-secret"))
    }

    func testMcpSessionIdIsDroppedFromNetworkSpanFields() {
        XCTAssertNil(SentryEventScrubber.scrubField(key: "Mcp-Session-Id", value: "mcp-session-secret"))
        XCTAssertNil(SentryEventScrubber.scrubField(key: "MCP-SESSION-ID", value: "mcp-session-secret"))
        XCTAssertEqual(SentryEventScrubber.scrubField(key: "method", value: "POST"), "POST")
    }

    func testSpanAddressAndQueryFieldsAreScrubbed() {
        XCTAssertEqual(
            SentryEventScrubber.scrubAddress("GET https://voucha.ai/search?q=private#token"),
            "GET https://voucha.ai/search"
        )
        XCTAssertNil(SentryEventScrubber.scrubField(key: "http.query", value: "q=private-search"))
        XCTAssertNil(SentryEventScrubber.scrubField(key: "http.query_string", value: "token=secret"))
        XCTAssertNil(SentryEventScrubber.scrubField(key: "http.fragment", value: "token=secret"))
        XCTAssertEqual(
            SentryEventScrubber.scrubField(key: "url", value: "https://voucha.ai/search?q=private-search"),
            "https://voucha.ai/search"
        )
        XCTAssertEqual(SentryEventScrubber.scrubField(key: "method", value: "GET"), "GET")
        XCTAssertEqual(
            SentryEventScrubber.scrubField(key: "from", value: "https://voucha.ai/x?otp=123456"),
            "https://voucha.ai/x"
        )
        XCTAssertTrue(SentryEventScrubber.keepsNumericField("status_code"))
        XCTAssertTrue(SentryEventScrubber.keepsNumericField("http.response.status_code"))
        XCTAssertFalse(SentryEventScrubber.keepsNumericField("http.query"))
        let scrubbed = SentryEventScrubber.scrub(SentryOutboundEvent(
            extras: ["request_url": "https://voucha.ai/search?q=private"],
            breadcrumbs: [SentryBreadcrumbDraft(category: "http", message: "GET /search?q=private")]
        ))
        XCTAssertEqual(scrubbed.extras["request_url"], "https://voucha.ai/search")
        XCTAssertEqual(scrubbed.breadcrumbs.first?.message, "GET /search")
    }

    private var secretEvent: SentryOutboundEvent {
        SentryOutboundEvent(
            accountId: "account-1",
            email: "ada@example.com",
            username: "ada",
            ipAddress: "203.0.113.4",
            requestURL: "https://voucha.ai/api/v1/session?code=123456",
            queryString: "code=123456",
            headers: [
                "Authorization": "Bearer mcp-token",
                "X-App-Attest-Assertion": "assertion",
                "Cookie": "dt=device; st=session",
                "Accept": "application/json",
                "Referer": "https://voucha.ai/reset?otp=123456"
            ],
            cookies: "dt=device; st=session",
            body: "{\"chat\":\"hello\"}",
            extras: ["chat": "hello", "email_code": "123456", "screen": "settings"],
            breadcrumbs: [
                SentryBreadcrumbDraft(category: "ui.text", message: "typed secret", data: ["text": "typed secret"]),
                SentryBreadcrumbDraft(
                    category: "http",
                    data: ["url": "https://voucha.ai/callback?token=oauth-token"]
                )
            ]
        )
    }
}

private final class RecordingSentryTransport: SentryEventTransport {
    private(set) var events: [SentryOutboundEvent] = []

    func send(_ event: SentryOutboundEvent) {
        events.append(event)
    }
}
