import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAPI
import VouchaModels
import XCTest

final class MemberMCPRetryCooldownTests: XCTestCase {
    override func tearDown() {
        RetryCooldownURLProtocol.configure(mode: "none", method: "tools/list")
        super.tearDown()
    }

    func testRefusalBlocksDispatchUntilMonotonicDeadlineWithoutRedispatch() async throws {
        for (mode, method) in [
            ("seconds", "tools/list"), ("date", "tools/list"), ("in-band", "tools/call"),
            ("seconds", "initialize"), ("seconds", "notifications/initialized")
        ] {
            let clock = RetryCooldownClock()
            RetryCooldownURLProtocol.configure(mode: mode, method: method)
            let client = try makeClient(clock)
            let first = try await limited { try await self.invoke(client, method: method) }
            XCTAssertEqual(first, 17)
            let admitted = RetryCooldownURLProtocol.requests()
            clock.jumpWallClock()
            clock.advance(10)
            let blocked = try await limited { try await self.invoke(client, method: method) }
            XCTAssertEqual(blocked, 7)
            XCTAssertEqual(RetryCooldownURLProtocol.requests(), admitted)
            clock.advance(7)
            XCTAssertEqual(RetryCooldownURLProtocol.requests(), admitted)

            try await invoke(client, method: method)
            XCTAssertGreaterThan(RetryCooldownURLProtocol.requests(), admitted)
            XCTAssertEqual(RetryCooldownURLProtocol.toolCalls(), method == "tools/call" ? 2 : 0)
        }
    }

    func testMissingAndInvalidDelayDoNotInventCooldown() async throws {
        for header in [nil, "invalid", "0", "-1"] as [String?] {
            let clock = RetryCooldownClock()
            RetryCooldownURLProtocol.configure(mode: "unknown", method: "tools/list", header: header)
            let client = try makeClient(clock)
            let failure = try await limited { try await self.invoke(client, method: "tools/list") }
            XCTAssertNil(failure)
            let admitted = RetryCooldownURLProtocol.requests()
            try await invoke(client, method: "tools/list")
            XCTAssertEqual(RetryCooldownURLProtocol.requests(), admitted + 1)
        }
    }

    func testCancellationCloseAndReplacementRemainAvailableDuringCooldown() async throws {
        let clock = RetryCooldownClock()
        RetryCooldownURLProtocol.configure(mode: "seconds", method: "tools/list")
        let client = try makeClient(clock)
        _ = try await limited { try await self.invoke(client, method: "tools/list") }
        let admitted = RetryCooldownURLProtocol.requests()
        let task = Task {
            withUnsafeCurrentTask { $0?.cancel() }
            return try await client.listTools(accessToken: "old-account")
        }
        do {
            _ = try await task.value
            XCTFail("Canceled work must not dispatch during a cooldown")
        } catch is CancellationError {}
        XCTAssertEqual(RetryCooldownURLProtocol.requests(), admitted)
        let stillLimited = try await limited { try await self.invoke(client, method: "tools/list") }
        XCTAssertEqual(stillLimited, 17)
        XCTAssertEqual(RetryCooldownURLProtocol.requests(), admitted)
        try await client.close(accessToken: "old-account")
        XCTAssertEqual(RetryCooldownURLProtocol.deletes(), 1)
        do {
            _ = try await client.listTools(accessToken: "old-account")
            XCTFail("Closed clients cannot resume")
        } catch MemberMCPClient.Failure.closed {}

        let replacement = try makeClient(clock)
        let beforeReplacement = RetryCooldownURLProtocol.requests()
        _ = try await replacement.listTools(accessToken: "new-account")
        XCTAssertEqual(RetryCooldownURLProtocol.requests(), beforeReplacement + 3)
    }

    func testLateShorterRefusalCannotShortenDeadlineOrThrottleReplacement() async throws {
        let clock = RetryCooldownClock()
        let firstHeld = expectation(description: "First request is in flight")
        let held = expectation(description: "Both ordinary requests are already in flight")
        RetryCooldownURLProtocol.configure(mode: "held", method: "tools/list") { count in
            if count == 1 { firstHeld.fulfill() }
            if count == 2 { held.fulfill() }
        }
        let original = try makeClient(clock)
        let first = Task { try await self.limited { try await self.invoke(original, method: "tools/list") } }
        await fulfillment(of: [firstHeld], timeout: 5)
        let second = Task { try await self.limited { try await self.invoke(original, method: "tools/list") } }
        await fulfillment(of: [held], timeout: 5)
        RetryCooldownURLProtocol.release(0, seconds: 17)
        _ = try await first.value
        clock.advance(10)
        RetryCooldownURLProtocol.stopHolding()
        let replacement = try makeClient(clock)
        _ = try await replacement.listTools(accessToken: "new-account")
        RetryCooldownURLProtocol.release(1, seconds: 2)
        let lateRefusal = try await second.value
        XCTAssertEqual(lateRefusal, 7)
        let blocked = try await limited { try await self.invoke(original, method: "tools/list") }
        XCTAssertEqual(blocked, 7)
        _ = try await replacement.listTools(accessToken: "new-account")
    }

    func testLargeAdvertisedDelayDoesNotOverflowOrDispatchAgain() async throws {
        RetryCooldownURLProtocol.configure(mode: "unknown", method: "tools/list", header: String(Int.max))
        let client = try makeClient(RetryCooldownClock())
        let first = try await limited { try await self.invoke(client, method: "tools/list") }
        XCTAssertEqual(first, Int.max)
        let admitted = RetryCooldownURLProtocol.requests()
        let second = try await limited { try await self.invoke(client, method: "tools/list") }
        XCTAssertEqual(second, Int.max)
        XCTAssertEqual(RetryCooldownURLProtocol.requests(), admitted)
    }

    private func makeClient(_ clock: RetryCooldownClock) throws -> MemberMCPClient {
        try MemberMCPClient(
            siteOrigin: XCTUnwrap(URL(string: "https://example.test")),
            protocolClasses: [RetryCooldownURLProtocol.self],
            retryUptime: { clock.uptime() }, retryDate: { clock.date() }
        )
    }

    private func invoke(_ client: MemberMCPClient, method: String) async throws {
        if method == "tools/call" {
            _ = try await client.callTool(name: "manage_bookmark", arguments: [:], accessToken: "account-access")
        } else {
            _ = try await client.listTools(accessToken: "account-access")
        }
    }

    private func limited(_ operation: () async throws -> Void) async throws -> Int? {
        do {
            try await operation()
            XCTFail("Expected a typed rate-limit refusal")
            return nil
        } catch let MemberMCPClient.Failure.rateLimited(seconds) {
            return seconds
        }
    }
}
