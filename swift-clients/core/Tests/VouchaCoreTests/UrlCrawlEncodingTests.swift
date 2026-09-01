import Foundation
@testable import VouchaModels
import XCTest

final class UrlCrawlEncodingTests: XCTestCase {
    func testPreservesEmbedResolutionFields() throws {
        let response = try makeVouchaDecoder().decode(
            UrlCrawlResponse.self,
            from: Data(
                """
                {
                  "crawl": {
                    "id": "crawl-1",
                    "embed_metadata": {
                      "title": "Example",
                      "provider": { "key": "youtube", "name": "YouTube" },
                      "player": { "url": "https://www.youtube.com/embed/example", "width": 640 }
                    },
                    "embed_oembed_url": "https://www.youtube.com/oembed",
                    "embed_oembed_resolved_at": "2026-09-01T00:00:00Z"
                  },
                  "og_image_sideload": null
                }
                """.utf8
            )
        )

        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .iso8601
        encoder.keyEncodingStrategy = .convertToSnakeCase
        let crawl = try crawlObject(from: encoder.encode(response))
        let metadata = try XCTUnwrap(crawl["embed_metadata"] as? [String: Any])
        let provider = try XCTUnwrap(metadata["provider"] as? [String: Any])

        XCTAssertEqual(metadata["title"] as? String, "Example")
        XCTAssertEqual(provider["key"] as? String, "youtube")
        XCTAssertEqual(crawl["embed_oembed_url"] as? String, "https://www.youtube.com/oembed")
        XCTAssertEqual(crawl["embed_oembed_resolved_at"] as? String, "2026-09-01T00:00:00Z")
    }

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
