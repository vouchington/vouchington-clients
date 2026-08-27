import Foundation
@testable import VouchaAPI
import XCTest

final class EndpointHostnamesAndReportsTests: XCTestCase {
    func testHostnameAndVoteEndpointsUseExpectedRoutes() {
        assertEndpoint(Endpoint.hostname(idOrHostname: "example.com"), path: "/api/v1/hostnames/example.com")
        assertEndpoint(
            Endpoint.voteHostname(hostnameId: "hostname 1", choice: .like),
            method: .PUT,
            path: "/api/v1/hostnames/hostname%201/vote",
            body: ["choice": "like"]
        )
    }

    func testReportEndpointEncodesModerationBody() {
        assertEndpoint(
            Endpoint.report(
                entityType: "url_hostname",
                entityId: "hostname-1",
                reason: "spam",
                note: "Bad actor",
                turnstileToken: "captcha-token"
            ),
            method: .POST,
            path: "/api/v1/reports",
            body: [
                "entityType": "url_hostname",
                "entityId": "hostname-1",
                "reason": "spam",
                "note": "Bad actor",
                "cf_turnstile_response": "captcha-token"
            ]
        )
    }

    func testUrlEndpointsUseExpectedRoutes() {
        assertEndpoint(Endpoint.url(urlId: "url 1"), path: "/api/v1/urls/url%201")
        XCTAssertEqual(Endpoint.urlCrawls(urlId: "url 1").path, "/api/v1/urls/url%201/crawls")
        XCTAssertEqual(
            Endpoint.urlCrawls(urlId: "url 1", after: "cursor-1", limit: 7).queryItems,
            [URLQueryItem(name: "limit", value: "7"), URLQueryItem(name: "after", value: "cursor-1")]
        )
        assertEndpoint(
            Endpoint.urlCrawl(urlId: "url 1", crawlId: "crawl 1"),
            path: "/api/v1/urls/url%201/crawls/crawl%201"
        )
        assertEndpoint(
            Endpoint.triggerUrlCrawl(urlId: "url 1"),
            method: .POST,
            path: "/api/v1/urls/url%201/crawl"
        )
    }

    func testRssFeedCrawlEndpointsUseScopedPathsAndCursor() {
        XCTAssertEqual(
            Endpoint.rssFeedCrawls(id: "feed 1", after: "cursor-1", limit: 7).path,
            "/api/v1/rss-feeds/feed%201/crawls"
        )
        XCTAssertEqual(
            Endpoint.rssFeedCrawls(id: "feed 1", after: "cursor-1", limit: 7).queryItems,
            [URLQueryItem(name: "limit", value: "7"), URLQueryItem(name: "after", value: "cursor-1")]
        )
        assertEndpoint(
            Endpoint.rssFeedCrawl(id: "feed 1", crawlId: "crawl 1"),
            path: "/api/v1/rss-feeds/feed%201/crawls/crawl%201"
        )
    }
}
