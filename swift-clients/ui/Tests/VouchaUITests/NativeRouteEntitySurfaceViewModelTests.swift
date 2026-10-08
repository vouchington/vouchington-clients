import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeRouteEntitySurfaceViewModelTests: NativeRouteSurfaceViewModelTestCase {
    func testEntityBrowseEmptyResponsesRenderNoResultsRows() async throws {
        for destination in [NativeRouteDestinationIdentifier.topicsBrowse, .domainsBrowse, .urlsBrowse] {
            CannedFeedURLProtocol.handlers = [
                expectedEntityListPath(for: destination): (Data(#"{"results":[]}"#.utf8), 200)
            ]
            CannedFeedURLProtocol.capturedURLs = []
            let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: destination), client: makeClient())

            await viewModel.load()

            XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, expectedEntityListPath(for: destination))
            XCTAssertEqual(viewModel.rows.first?.detail, "No results")
        }
    }

    func testUsersBrowseWithQueryLoadsUserSearchResults() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users"] = (
            Data(
                """
                {
                  "results": [{"id":"user-1","username":"alice"}],
                  "page_info": {
                    "has_next_page": false,
                    "start_cursor": null,
                    "end_cursor": null
                  }
                }
                """.utf8
            ),
            200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/users?q=alice"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/users")
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.query, "q=alice&limit=25")
        XCTAssertEqual(viewModel.rows.first, verbatimRow(icon: "person", title: "alice", detail: "user-1"))
    }

    func testTopicsBrowseRendersVoteSidecarSummary() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topics"] = (
            Data("""
            {
              "results": [{ "id": "topic-1" }],
              "topics": {
                "topic-1": { "id": "topic-1", "name": "Swift", "slug": "swift", "topic_type": "topic" }
              },
              "topic_elections": {
                "topic-1": {
                  "votes_score_net": 4,
                  "votes_count_up": 6,
                  "votes_count_down": 2,
                  "my_vote": "dislike"
                }
              },
              "election_votes": {
                "topic-1": {
                  "__entity_type": "election_vote",
                  "entity_id": "topic-1",
                  "user_id": "user-1",
                  "choice": "like",
                  "created_at": "2026-01-01T00:00:00Z"
                }
              }
            }
            """.utf8),
            200
        )
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .topicsBrowse), client: makeClient())

        await viewModel.load()

        XCTAssertEqual(viewModel.rows.first?.title, "Swift")
        XCTAssertEqual(viewModel.rows.first?.detail, "6 positive votes · 2 negative votes")
        XCTAssertEqual(viewModel.myVotesByTopicId["topic-1"], .like)
    }

    func testHostnameUrlUserDetailsLoadNativeResponses() async throws {
        let fixtures: [EntityDetailFixture] = [
            .init(
                destination: .domainDetail,
                routePath: "/domain/example.com",
                endpointPath: "/api/v1/hostnames/example.com",
                response: """
                {
                  "hostname": {
                    "id": "domain-1",
                    "hostname": "example.com",
                    "topic_id": "topic-1",
                    "is_blocked": false,
                    "is_crawlable": true,
                    "should_follow_link_rel": true
                  },
                  "hostname_election": {
                    "votes_score_net": 10,
                    "votes_count_up": 12,
                    "votes_count_down": 2,
                    "my_vote": "like"
                  },
                  "election_vote": {
                    "__entity_type": "election_vote",
                    "entity_id": "hostname-1",
                    "user_id": "user-1",
                    "choice": "like",
                    "created_at": "2026-01-01T00:00:00Z"
                  },
                  "rss_feeds": [
                    {
                      "id": "feed-1",
                      "title": "Example Feed",
                      "feed_type": "article",
                      "rss_feed_url": { "url": "https://example.com/feed.xml" }
                    }
                  ],
                  "top_urls": [
                    { "id": "url-1", "url": "https://example.com/post" }
                  ],
                  "topic": {
                    "id": "topic-1",
                    "name": "Example Topic",
                    "slug": "example-topic",
                    "topic_type": "topic"
                  }
                }
                """,
                expectedRow: verbatimRow(icon: "globe", title: "example.com", detail: "Crawlable · Followable"),
                assertRouteDetail: false
            ),
            .init(
                destination: .urlDetail,
                routePath: "/url/url-1",
                endpointPath: "/api/v1/urls/url-1",
                response: """
                {
                  "can_trigger_crawl": true,
                  "can_view_crawl_history": true,
                  "can_view_latest_crawl": true,
                  "latest_crawl": {
                    "id": "crawl-1",
                    "created_at": "2026-06-01T12:00:00Z",
                    "completed_at": "2026-06-01T12:00:05Z",
                    "response_status_code": 200,
                    "title": "Native URL"
                  },
                  "url": {
                    "id": "url-1",
                    "url": "https://example.com/post",
                    "hostname": { "id": "domain-1", "hostname": "example.com" },
                    "pathname": "/post",
                    "search_params": {},
                    "canonical_url_id": null,
                    "url_type": "url"
                  },
                  "url_type": "url"
                }
                """,
                expectedRow: verbatimRow(
                    icon: "link",
                    title: "https://example.com/post",
                    detail: "example.com · /post · url"
                ),
                assertRouteDetail: false,
                extraHandlers: [
                    "/api/v1/urls/url-1/crawls": (
                        Data(
                            #"{"results":[{"id":"crawl-1","created_at":"2026-06-01T12:00:00Z","response_status_code":200,"completed_at":"2026-06-01T12:00:05Z"}],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}"#
                                .utf8
                        ),
                        200
                    )
                ]
            )
        ]

        for fixture in fixtures {
            CannedFeedURLProtocol.handlers = [fixture.endpointPath: (Data(fixture.response.utf8), 200)]
                .merging(fixture.extraHandlers) { current, _ in current }
            CannedFeedURLProtocol.capturedURLs = []
            let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: fixture.routePath)?.match)
            let viewModel = try NativeRouteSurfaceViewModel(
                entry: entry(for: fixture.destination),
                client: makeClient(),
                routeMatch: match
            )

            await viewModel.load()

            XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.map(\.path).contains(fixture.endpointPath))
            XCTAssertEqual(viewModel.rows.first, fixture.expectedRow)
            if fixture.assertRouteDetail {
                XCTAssertEqual(viewModel.rows.last?.title, "Matched route")
                XCTAssertEqual(viewModel.rows.last?.detail, fixture.routePath)
            }
        }

        CannedFeedURLProtocol.handlers = [
            "/api/v1/users/alice": (
                Data(#"{"user":{"id":"user-1","username":"alice","markdown":"Native user"},"profile_links":[]}"#.utf8),
                200
            )
        ]
        CannedFeedURLProtocol.capturedURLs = []
        let userRoute = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice"))
        let userViewModel = try NativeRouteSurfaceViewModel(
            entry: userRoute.entry,
            client: makeClient(),
            routeMatch: userRoute.match
        )

        await userViewModel.load()

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.map(\.path).contains("/api/v1/users/alice"))
        XCTAssertEqual(userViewModel.userProfile.header?.user.username, "alice")
        XCTAssertEqual(userViewModel.userProfile.header?.user.markdown, "Native user")
        XCTAssertEqual(userViewModel.userProfile.scope, .overview)
    }

    func testURLDetailLoadsRecentCrawlRows() async throws {
        CannedFeedURLProtocol.handlers = [
            "/api/v1/urls/url-1": (
                Data(
                    """
                    {
                      "can_trigger_crawl": true,
                      "can_view_crawl_history": true,
                      "can_view_latest_crawl": true,
                      "latest_crawl": {
                        "id": "crawl-1",
                        "created_at": "2026-06-01T12:00:00Z",
                        "completed_at": "2026-06-01T12:00:05Z",
                        "response_status_code": 200,
                        "title": "Native URL"
                      },
                      "url": {
                        "id": "url-1",
                        "url": "https://example.com/post",
                        "pathname": "/post",
                        "search_params": {},
                        "url_type": "url"
                      },
                      "url_type": "url"
                    }
                    """.utf8
                ),
                200
            ),
            "/api/v1/urls/url-1/crawls": (
                Data(
                    #"{"results":[{"id":"crawl-1","created_at":"2026-06-01T12:00:00Z","response_status_code":200,"completed_at":"2026-06-01T12:00:05Z"}],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}"#
                        .utf8
                ),
                200
            )
        ]
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/url/url-1/crawls")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .urlDetail),
            client: makeClient(),
            routeMatch: match
        )

        await viewModel.load()

        XCTAssertEqual(Set(CannedFeedURLProtocol.capturedURLs.map(\.path)), [
            "/api/v1/urls/url-1",
            "/api/v1/urls/url-1/crawls"
        ])
        XCTAssertEqual(
            viewModel.rows[1],
            verbatimRow(icon: "clock.arrow.circlepath", title: "Crawl history", detail: "Crawl history available")
        )
        XCTAssertEqual(
            viewModel.rows[2],
            verbatimRow(icon: "doc.text.magnifyingglass", title: "Native URL", detail: "HTTP 200")
        )
        let utc = try XCTUnwrap(TimeZone(secondsFromGMT: 0))
        XCTAssertEqual(viewModel.rows[3].icon, "doc.text.magnifyingglass")
        XCTAssertEqual(
            viewModel.rows[3].localizedTitle(
                locale: Locale(identifier: "en"),
                timeZone: utc
            ),
            uiEnglishDate(Date(timeIntervalSince1970: 1_780_315_200))
        )
        XCTAssertEqual(viewModel.rows[3].detail, "HTTP 200")
        XCTAssertEqual(
            viewModel.rows.last,
            verbatimRow(icon: "arrow.triangle.2.circlepath", title: "Trigger crawl", detail: "Start a fresh crawl")
        )
    }

    func testURLCrawlDetailLoadsMatchedCrawl() async throws {
        CannedFeedURLProtocol.handlers = [
            "/api/v1/urls/url-1": (
                Data(
                    """
                    {
                      "can_trigger_crawl": false,
                      "can_view_crawl_history": true,
                      "can_view_latest_crawl": true,
                      "latest_crawl": null,
                      "url": {
                        "id": "url-1",
                        "url": "https://example.com/post",
                        "pathname": "/post",
                        "search_params": {},
                        "url_type": "url"
                      },
                      "url_type": "url"
                    }
                    """.utf8
                ),
                200
            ),
            "/api/v1/urls/url-1/crawls/crawl-1": (
                Data(
                    #"{"crawl":{"id":"crawl-1","title":"Fetched page","created_at":"2026-06-01T12:00:00Z","completed_at":null,"response_status_code":404,"language":"en","markdown":"Body","meta_tags":{"og:title":"Native"}},"og_image_sideload":"/sideload/image"}"#
                        .utf8
                ),
                200
            )
        ]
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/url/url-1/crawls/crawl-1")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .urlDetail),
            client: makeClient(),
            routeMatch: match
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/urls/url-1",
            "/api/v1/urls/url-1/crawls/crawl-1"
        ])
        XCTAssertEqual(
            viewModel.rows.first,
            verbatimRow(icon: "doc.text.magnifyingglass", title: "Fetched page", detail: "HTTP 404")
        )
        XCTAssertTrue(viewModel.rows.contains(verbatimRow(
            icon: "photo",
            title: "Open graph image",
            detail: "/sideload/image"
        )))
        XCTAssertTrue(viewModel.rows.contains(verbatimRow(icon: "text.alignleft", title: "Content", detail: "Body")))
    }

    func testHostnameDetailUsesTrustAndStatusResponse() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/hostnames/example.org"] = (
            Data(
                """
                {
                  "hostname": {
                    "id": "domain-1",
                    "hostname": "example.org",
                    "is_blocked": true,
                    "is_crawlable": false,
                    "should_follow_link_rel": false
                  },
                  "hostname_election": {
                    "votes_score_net": 4,
                    "votes_count_up": 6,
                    "votes_count_down": 2,
                    "my_vote": null
                  }
                }
                """.utf8
            ),
            200
        )
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/domain/example.org")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .domainDetail),
            client: makeClient(),
            routeMatch: match
        )

        await viewModel.load()

        XCTAssertEqual(
            viewModel.rows.first,
            verbatimRow(icon: "globe", title: "example.org", detail: "Blocked · Not crawlable · Not followable")
        )
        XCTAssertEqual(viewModel.rows[1].title, "Trust vote")
        XCTAssertEqual(viewModel.rows[1].detail, "6 positive votes · 2 negative votes")
    }

    func testCompareRouteLoadsMatchedTopicComparison() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topics/compare"] = (
            Data(
                #"{"topics":{"topic-1":{"id":"topic-1","name":"Swift","slug":"swift","topic_type":"tag"},"topic-2":{"id":"topic-2","name":"Rust","slug":"rust","topic_type":"tag"}}}"#
                    .utf8
            ),
            200
        )
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/compare/swift-vs-rust")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .compare),
            client: makeClient(),
            routeMatch: match
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/topics/compare")
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.query, "slugs=swift,rust")
        XCTAssertEqual(viewModel.rows.first?.title, "Swift vs Rust")
    }

    func testDomainsCompareRouteHonorsIdsQuery() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/hostnames/compare"] = (
            Data(
                #"{"hostnames":{"domain-1":{"id":"domain-1","hostname":"example.com","topic_id":"topic-1"},"domain-2":{"id":"domain-2","hostname":"example.org","topic_id":null}}}"#
                    .utf8
            ),
            200
        )
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/domains/compare?ids=domain-1,domain-2")?
            .match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .compare),
            client: makeClient(),
            routeMatch: match
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/hostnames/compare")
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.query, "ids=domain-1,domain-2")
        XCTAssertEqual(viewModel.rows.first?.title, "example.com vs example.org")
    }

    func testCompareWithoutRouteParamsLoadsComparableEntityCounts() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topics"] = (
            Data(#"{"results":[{"id":"topic-1"},{"id":"topic-2"}]}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/hostnames"] = (
            Data(#"{"results":[{"id":"domain-1"}]}"#.utf8),
            200
        )
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .compare), client: makeClient())

        await viewModel.load()

        XCTAssertEqual(
            Set(CannedFeedURLProtocol.capturedURLs.map(\.path)),
            Set(["/api/v1/topics", "/api/v1/hostnames"])
        )
        XCTAssertEqual(viewModel.rows, [
            verbatimRow(icon: "tag", title: "Topics", detail: "2 topics"),
            verbatimRow(icon: "globe", title: "Domains", detail: "1 domain")
        ])
    }

    private func expectedEntityListPath(for destination: NativeRouteDestinationIdentifier) -> String {
        switch destination {
        case .topicsBrowse:
            return "/api/v1/topics"
        case .domainsBrowse:
            return "/api/v1/hostnames"
        case .urlsBrowse:
            return "/api/v1/urls"
        default:
            XCTFail("Unexpected entity destination \(destination)")
            return ""
        }
    }
}

private struct EntityDetailFixture {
    let destination: NativeRouteDestinationIdentifier
    let routePath: String
    let endpointPath: String
    let response: String
    let expectedRow: NativeRouteDestinationRow
    var assertRouteDetail = true
    var extraHandlers: [String: (Data, Int)] = [:]
}
