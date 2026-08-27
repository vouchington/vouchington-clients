import Foundation
@testable import VouchaAPI
import VouchaModels
import XCTest

final class ImportExportEndpointTests: XCTestCase {
    override func setUp() {
        CapturingURLProtocol.responseStatusCode = 200
        CapturingURLProtocol.responseData = Data()
        CapturingURLProtocol.lastRequestURL = nil
        CapturingURLProtocol.capturedRequestHeaders = [:]
    }

    func testTopicImportAndExportEndpoints() {
        assertEndpoint(
            .importTopics(["Travel", "Local News"]),
            method: .POST,
            path: "/api/v1/my/import/topics",
            body: ["names": ["Travel", "Local News"]]
        )
        assertEndpoint(.exportTopics, path: "/api/v1/my/export/topics")
    }

    func testSourceImportUsesMutuallyExclusiveInputAndFollow() {
        assertEndpoint(
            .importRssFeeds(.urls(["https://example.test/feed.xml"])),
            method: .POST,
            path: "/api/v1/my/import/rss-feeds",
            body: ["urls": ["https://example.test/feed.xml"], "follow": true]
        )
        assertEndpoint(
            .importRssFeeds(.csv("url\nhttps://example.test/feed.xml")),
            method: .POST,
            path: "/api/v1/my/import/rss-feeds",
            body: ["csv": "url\nhttps://example.test/feed.xml", "follow": true]
        )
        assertEndpoint(
            .importRssFeeds(.opml("<opml/>")),
            method: .POST,
            path: "/api/v1/my/import/rss-feeds",
            body: ["opml": "<opml/>", "follow": true]
        )
    }

    func testSourceStatusAndExportEndpoints() {
        assertEndpoint(
            .rssFeedImportStatus(importId: "70000000-0000-7000-8000-000000000001"),
            path: "/api/v1/my/import/rss-feeds/70000000-0000-7000-8000-000000000001"
        )
        let csv = Endpoint.exportRssFeeds(feedType: .podcast, format: .csv)
        XCTAssertEqual(
            csv.queryItems,
            [.init(name: "feed_type", value: "podcast"), .init(name: "format", value: "csv")]
        )
        XCTAssertEqual(csv.headers["Accept"], "text/csv")
        let opml = Endpoint.exportRssFeeds(feedType: nil, format: .opml)
        XCTAssertTrue(opml.queryItems.isEmpty)
        XCTAssertEqual(opml.headers["Accept"], "text/xml")
    }

    func testRawDataTransportPreservesBytesAndHeaders() async throws {
        let expected = Data("title,url\nExample,https://example.test".utf8)
        CapturingURLProtocol.responseData = expected
        let client = APIClient(protocolClasses: [CapturingURLProtocol.self], bootstrapSession: false)

        let data = try await client.data(for: .exportRssFeeds(feedType: .article, format: .csv))

        XCTAssertEqual(data, expected)
        XCTAssertEqual(CapturingURLProtocol.capturedRequestHeaders["Accept"], "text/csv")
    }

    func testRawDataTransportMapsHTTPFailures() async {
        CapturingURLProtocol.responseStatusCode = 413
        CapturingURLProtocol.responseData = Data(#"{"error":"Too large","code":"SYNC_EXPORT_TOO_LARGE"}"#.utf8)
        let client = APIClient(protocolClasses: [CapturingURLProtocol.self], bootstrapSession: false)

        await XCTAssertThrowsErrorAsync { try await client.data(for: .exportTopics) }
    }

    func testImportExportFixturesRoundTripWithoutDroppingFields() throws {
        try assertFixtureCoversDTO(
            "native.import-export.rss-feeds.submit.default",
            as: RssFeedImportSubmission.self,
            ignoring: ["import.completed_at"]
        )
        try assertFixtureCoversDTO(
            "native.import-export.rss-feeds.status.retrying",
            as: RssFeedImportStatus.self,
            ignoring: ["import.completed_at"]
        )
        try assertFixtureCoversDTO("native.import-export.rss-feeds.status.partial", as: RssFeedImportStatus.self)
        try assertFixtureCoversDTO("native.import-export.topics.import.outcomes", as: TopicImportResponse.self)
        try assertFixtureCoversDTO("native.import-export.topics.export.default", as: TopicExportResponse.self)
    }
}

private func XCTAssertThrowsErrorAsync(
    _ expression: () async throws -> some Any,
    file: StaticString = #filePath,
    line: UInt = #line
) async {
    do {
        _ = try await expression()
        XCTFail("Expected error", file: file, line: line)
    } catch {}
}
