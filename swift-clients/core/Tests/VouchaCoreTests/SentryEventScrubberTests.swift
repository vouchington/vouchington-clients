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
                "Accept": "application/json"
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
