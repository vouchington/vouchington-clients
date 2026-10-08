import VouchaAPI
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeRouteContractFieldTests: XCTestCase {
    func testHostnameSummaryDecodesCurrentPolicyFields() throws {
        let summary = try JSONDecoder.vouchaFixtureDecoder.decode(
            NativeHostnameSummary.self,
            from: Data(
                #"{"id":"host-1","hostname":"example.com","topic_id":"topic-1","is_blocked":true,"is_crawlable":false,"should_follow_link_rel":true}"#
                    .utf8
            )
        )

        XCTAssertEqual(summary.id, "host-1")
        XCTAssertEqual(summary.topicId, "topic-1")
        XCTAssertTrue(summary.blocked)
        XCTAssertEqual(summary.crawlable, false)
        XCTAssertEqual(summary.linkRelFollow, true)
    }

    func testCrawlSummaryDecodesCurrentLanguageAndExistingFields() throws {
        let summary = try JSONDecoder.vouchaFixtureDecoder.decode(
            NativeUrlCrawlSummary.self,
            from: Data(
                #"{"id":"crawl-1","language":"fr","response_status_code":200,"completed_at":"2026-01-01T00:00:00Z","created_at":"2025-12-31T00:00:00Z","title":"Title","markdown":"Body","meta_tags":{"description":"Summary"}}"#
                    .utf8
            )
        )

        XCTAssertEqual(summary.lang, "fr")
        XCTAssertEqual(summary.responseStatusCode, 200)
        XCTAssertEqual(summary.completedAt, "2026-01-01T00:00:00Z")
        XCTAssertEqual(summary.createdAt, "2025-12-31T00:00:00Z")
        XCTAssertEqual(summary.title, "Title")
        XCTAssertEqual(summary.markdown, "Body")
        XCTAssertNotNil(summary.metaTags?["description"])
    }
}
