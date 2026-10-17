import Foundation
@testable import VouchaAPI
import XCTest

final class EngineeringQueueDomainTests: XCTestCase {
    func testDomainRoutesEscapeQueueAndDomainAndPreserveOptionalReason() {
        assertEndpoint(.mqQueueDomains(name: "back ground"), path: "/api/v1/mq/queues/back%20ground/domains")
        assertEndpoint(
            .mqPauseDomain(queue: "back ground", domain: "rss/imports", reason: "Provider outage"),
            method: .POST,
            path: "/api/v1/mq/queues/back%20ground/domains/rss%2Fimports/pause",
            body: ["reason": "Provider outage"]
        )
        assertEndpoint(
            .mqPauseDomain(queue: "background", domain: "rss"),
            method: .POST,
            path: "/api/v1/mq/queues/background/domains/rss/pause"
        )
        assertEndpoint(
            .mqResumeDomain(queue: "background", domain: "rss"),
            method: .POST,
            path: "/api/v1/mq/queues/background/domains/rss/resume"
        )
        assertEndpoint(
            .mqRetryFailedDomain(queue: "background", domain: "rss"),
            method: .POST,
            path: "/api/v1/mq/queues/background/domains/rss/retry-failed"
        )
    }

    func testDomainFixturesPreserveCappedCountsAndPauseMetadata() throws {
        let response = try makeVouchaDecoder().decode(
            EngineeringQueueDomainsResponse.self,
            from: ApiFixtureLoader.data("web.admin.mq.queues.domains.default")
        )
        XCTAssertFalse(response.domains.isEmpty)
        XCTAssertTrue(response.domains.contains { !$0.waiting.complete || !$0.failed.complete })
        XCTAssertTrue(response.domains.contains { $0.paused && $0.reason != nil })
        let retry = try makeVouchaDecoder().decode(
            EngineeringRetryDomainResponse.self,
            from: ApiFixtureLoader.data("web.admin.mq.queues.domains.retry-failed.default")
        )
        XCTAssertTrue(retry.success)
        XCTAssertGreaterThanOrEqual(retry.retried, 0)
    }
}
