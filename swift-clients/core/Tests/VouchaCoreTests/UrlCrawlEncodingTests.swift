import Foundation
@testable import VouchaModels
import XCTest

final class UrlCrawlEncodingTests: XCTestCase {
    func testPreservesPrivilegedExplicitNullCrawlFieldsWhileLeavingPaidOmissionsAbsent() throws {
        let decoder = makeVouchaDecoder()
        let privileged = try decoder.decode(
            UrlCrawlResponse.self,
            from: ApiFixtureLoader.data("native.url-crawl.default")
        )
        let paid = try decoder.decode(
            UrlCrawlResponse.self,
            from: ApiFixtureLoader.data("native.paid.url-crawl.default")
        )

        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        let privilegedCrawl = try crawlObject(from: encoder.encode(privileged))
        let paidCrawl = try crawlObject(from: encoder.encode(paid))

        for key in [
            "embeddings_generated_at",
            "etag",
            "html_sha256",
            "html_snapshot_uploaded_at",
            "last_modified_at",
            "network_error",
            "redirect_url_id"
        ] {
            XCTAssertTrue(privilegedCrawl[key] is NSNull, "Expected privileged \(key) to remain null")
            XCTAssertNil(paidCrawl[key], "Expected paid \(key) to remain absent")
        }

        for key in ["crawler_id", "links", "markdown", "meta_tags", "request_headers", "response_headers", "url_id"] {
            XCTAssertNil(paidCrawl[key], "Expected paid \(key) to remain absent")
        }
    }

    private func crawlObject(from data: Data) throws -> [String: Any] {
        let response = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        return try XCTUnwrap(response["crawl"] as? [String: Any])
    }
}
