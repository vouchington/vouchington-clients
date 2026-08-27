import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class NativeRouteSurfaceViewModelDiscoveryTests: NativeRouteSurfaceViewModelTestCase {
    func testGroupedSearchSectionsRenderAllNativeResultKinds() throws {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        let response = try decoder.decode(
            OmnisearchResponse.self,
            from: Data("""
            {
              "topics": [{ "id": "topic-1", "name": "Credit Cards", "slug": "credit-cards", "topic_type": "spending_category" }],
              "posts": [
                { "id": "post-1", "post_type": "blog_post", "title": "Blog result" },
                { "id": "post-2", "post_type": "data_point", "title": "Data result" },
                { "id": "post-3", "post_type": "unknown_kind", "title": "Unknown result" }
              ],
              "news": [{ "id": "news-1", "url": "https://example.com/news", "title": "News result", "feed_title": "Example Feed" }],
              "domains": [{ "id": "domain-1", "hostname": "example.com" }],
              "communities": [
                { "id": "community-1", "name": "Native Club", "slug": "native-club", "bookmarked": true },
                { "id": "community-2", "name": "Open Club", "slug": "open-club", "bookmarked": false }
              ]
            }
            """.utf8)
        )
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .webSearch), client: makeClient())

        let sections = viewModel.groupedSearchSections(from: response)

        XCTAssertEqual(
            sections.map { uiEnglish($0.title) },
            ["Topics", "Posts", "News", "Domains", "Communities"]
        )
        XCTAssertEqual(
            sections[0].rows.first,
            verbatimRow(icon: "tag", title: "Topic: Credit Cards", detail: "spending_category")
        )
        XCTAssertEqual(sections[1].rows.map(\.detail), ["Blog post", "Data point", "unknown_kind"])
        XCTAssertEqual(
            sections[2].rows.first,
            verbatimRow(icon: "newspaper", title: "Article: News result", detail: "Example Feed")
        )
        XCTAssertEqual(
            sections[3].rows.first,
            verbatimRow(icon: "globe", title: "Domain: example.com", detail: "Domain")
        )
        XCTAssertEqual(sections[4].rows.map(\.detail), ["Bookmarked", "open-club"])
    }

    func testFediverseSearchLoadsFediverseEndpoint() async throws {
        let surface = try NativeRouteDestinationSurface(
            entry: entry(for: .fediverseSearch),
            client: nil,
            routeMatch: nil,
            routeQuery: "query=fediverse%20swift",
            isSignedIn: false,
            showSignIn: {}
        )
        XCTAssertEqual(surface.routeSearchQuery, "fediverse swift")

        let qSurface = try NativeRouteDestinationSurface(
            entry: entry(for: .fediverseSearch),
            client: nil,
            routeMatch: nil,
            routeQuery: "q=peertube%20swift",
            isSignedIn: false,
            showSignIn: {}
        )
        XCTAssertEqual(qSurface.routeSearchQuery, "peertube swift")

        CannedFeedURLProtocol.handlers["/api/v1/fediverse/search"] = (
            Data("""
            {
              "buckets": [
                {
                  "provider": "peertube",
                  "status": "ok",
                  "items": [
                    {
                      "provider": "peertube",
                      "result_type": "video",
                      "external_url": "https://videos.example/watch/1",
                      "title": "Native video",
                      "summary": "A useful video.",
                      "author_name": "Alice",
                      "source_hostname": "videos.example"
                    }
                  ]
                }
              ]
            }
            """.utf8),
            200
        )
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .fediverseSearch), client: makeClient())

        await viewModel.search(query: "native video")

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/fediverse/search")
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.query, "q=native%20video&limit=10")
        XCTAssertEqual(viewModel.searchSections.map { uiEnglish($0.title) }, ["PeerTube"])
        XCTAssertEqual(
            viewModel.rows.first,
            verbatimRow(
                icon: "play.rectangle",
                title: "Native video",
                detail: "Alice on videos.example",
                externalURL: URL(string: "https://videos.example/watch/1")
            )
        )
    }

    func testFediverseSearchPreservesProviderFilter() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/fediverse/search"] = (
            Data(#"{ "buckets": [] }"#.utf8),
            200
        )
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(
            for: "/fediverse?provider=peertube&q=native"
        )?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .fediverseSearch),
            client: makeClient(),
            routeMatch: match
        )

        await viewModel.search(query: "native")

        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.first?.query,
            "q=native&providers=peertube&limit=10"
        )
    }

    func testFediverseSearchPreservesLemmyProviderFilter() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/fediverse/search"] = (
            Data(#"{ "buckets": [] }"#.utf8),
            200
        )
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(
            for: "/fediverse?provider=lemmy&q=native"
        )?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .fediverseSearch),
            client: makeClient(),
            routeMatch: match
        )

        await viewModel.search(query: "native")

        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.first?.query,
            "q=native&providers=lemmy&limit=10"
        )
    }

    func testFediverseInstancesLoadDedicatedDirectory() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/fediverse/instances"] = (
            ApiFixtureLoader.data("native.fediverse.instances.default"), 200
        )
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .fediverseInstances), client: makeClient())

        await viewModel.load()

        XCTAssertEqual(viewModel.rows.map(\.title), ["social.example", "unknown.example"])
        XCTAssertEqual(viewModel.rows.first?.detail, "mastodon 4.4.0 · Trust Trusted")
        XCTAssertEqual(viewModel.rows.last?.detail, "Unclassified · Trust Unrated")
        XCTAssertEqual(viewModel.rows.first?.targetPath, "/instance/social-example")
        XCTAssertTrue(viewModel.fediverseInstancePageInfo?.hasNextPage == true)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.query, "limit=25&sort=best")
    }

    func testFediverseSearchDropsUnsafeExternalURLs() throws {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        let response = try decoder.decode(
            FediverseSearchResponse.self,
            from: Data("""
            {
              "buckets": [
                {
                  "provider": "mastodon",
                  "status": "ok",
                  "items": [
                    {
                      "provider": "mastodon",
                      "result_type": "post",
                      "external_url": "javascript:alert(1)",
                      "title": "Unsafe post",
                      "summary": "",
                      "source_hostname": "social.example"
                    }
                  ]
                }
              ]
            }
            """.utf8)
        )
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .fediverseSearch), client: makeClient())

        let sections = viewModel.groupedFediverseSections(from: response)

        XCTAssertNil(sections.first?.rows.first?.externalURL)
    }

    func testFediverseSearchFallsBackToProviderWhenSourceHostnameIsMissing() throws {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        let response = try decoder.decode(
            FediverseSearchResponse.self,
            from: Data("""
            {
              "buckets": [
                {
                  "provider": "mastodon",
                  "status": "ok",
                  "items": [
                    {
                      "provider": "mastodon",
                      "result_type": "post",
                      "external_url": "https://social.example/@alice/1",
                      "title": "Fallback post",
                      "summary": "",
                      "author_name": "Alice"
                    }
                  ]
                }
              ]
            }
            """.utf8)
        )
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .fediverseSearch), client: makeClient())

        let sections = viewModel.groupedFediverseSections(from: response)

        XCTAssertEqual(sections.first?.rows.first?.detail, "Alice on Mastodon")
    }

    func testFediverseSearchLabelsProfileResultsWithPersonIcon() throws {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        let response = try decoder.decode(
            FediverseSearchResponse.self,
            from: Data("""
            {
              "buckets": [
                {
                  "provider": "mastodon",
                  "status": "ok",
                  "items": [
                    {
                      "provider": "mastodon",
                      "result_type": "profile",
                      "external_url": "https://social.example/@alice",
                      "title": "Alice",
                      "summary": "",
                      "source_hostname": "social.example"
                    }
                  ]
                }
              ]
            }
            """.utf8)
        )
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .fediverseSearch), client: makeClient())

        let sections = viewModel.groupedFediverseSections(from: response)

        XCTAssertEqual(sections.first?.rows.first?.icon, "person.crop.circle")
    }

    func testFediverseSearchKeepsEmptyErrorBuckets() throws {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        let response = try decoder.decode(
            FediverseSearchResponse.self,
            from: Data("""
            {
              "buckets": [
                {
                  "provider": "peertube",
                  "status": "error",
                  "items": [],
                  "error_code": "provider_timeout"
                }
              ]
            }
            """.utf8)
        )
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .fediverseSearch), client: makeClient())

        let sections = viewModel.groupedFediverseSections(from: response)

        XCTAssertEqual(sections.map { uiEnglish($0.title) }, ["PeerTube"])
        XCTAssertEqual(
            sections.first?.rows.first,
            verbatimRow(
                icon: "exclamationmark.triangle",
                title: "PeerTube is unavailable.",
                detail: "PeerTube is unavailable."
            )
        )
    }

    func testFediverseSearchRetainsPartialItemsWithoutExposingProviderErrorCode() throws {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        let response = try decoder.decode(FediverseSearchResponse.self, from: Data("""
        {"buckets":[{"provider":"mastodon","status":"error","error_code":"internal_secret_code","items":[{
          "provider":"mastodon","result_type":"post","external_url":"https://social.example/p/1","title":"Result",
          "summary":""
        }]}]}
        """.utf8))
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .fediverseSearch), client: makeClient())
        let rows = try XCTUnwrap(viewModel.groupedFediverseSections(from: response).first).rows
        XCTAssertEqual(rows.map(\.title), ["Result", "Some results may be unavailable."])
        XCTAssertFalse(rows.map(\.detail).contains("internal_secret_code"))
    }

    func testReferralLinksFeedLoadsFollowUsersEndpoint() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/referral_links/follow_users"] = (referralLinksData, 200)
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .feedReferralLinks), client: makeClient())

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/feeds/referral_links/follow_users")
        XCTAssertEqual(viewModel.rows.first, verbatimRow(icon: "link", title: "Native card", detail: "user-1"))
    }

    func testReferralLinksFeedLoadsMutualFollowsEndpoint() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/referral_links/mutual_follows"] = (referralLinksData, 200)
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/feed/referral-links/mutual")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .feedReferralLinks),
            client: makeClient(),
            routeMatch: match
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/feeds/referral_links/mutual_follows")
        XCTAssertEqual(viewModel.rows.first?.title, "Native card")
    }

    func testTopicRecommendationsLoadsNativeEndpoint() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topic-recommendations"] = (
            Data("""
            {
              "results": [{ "id": "topic-rec-1", "entity_id": "post-1" }],
              "posts": {
                "post-1": {
                  "id": "post-1",
                  "slug": "native-recommendation",
                  "post_type": "topic_recommendation",
                  "title": "Native recommendation",
                  "markdown": "Create this topic.",
                  "html": null,
                  "parent_id": null,
                  "root_id": null,
                  "created_by_id": "user-1",
                  "created_at": "2026-01-01T00:00:00Z",
                  "broadcast": "everyone",
                  "privacy": "public",
                  "is_anonymous": false,
                  "community_id": null,
                  "clearance_status": null
                }
              },
              "page_info": { "has_next_page": false, "end_cursor": null, "start_cursor": null }
            }
            """.utf8),
            200
        )
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .topicRecommendations), client: makeClient())

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/topic-recommendations")
        XCTAssertEqual(
            viewModel.rows.first,
            verbatimRow(icon: "lightbulb", title: "Native recommendation", detail: "Create this topic.")
        )
    }

    func testTopicRecommendationEditLoadsMatchedRecommendation() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topic-recommendations/topic-rec-1"] = (
            Data("""
            {
              "post": {
                "id": "topic-rec-1",
                "slug": "native-recommendation",
                "post_type": "topic_recommendation",
                "title": "Native recommendation",
                "markdown": "Update this topic.",
                "created_at": "2026-01-01T00:00:00Z",
                "created_by_id": "user-1"
              }
            }
            """.utf8),
            200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic-recommendations/topic-rec-1/edit"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/topic-recommendations/topic-rec-1")
        XCTAssertEqual(viewModel.rows.first?.detail, "Update this topic.")
    }

    private var referralLinksData: Data {
        Data("""
        {
          "results": [
            {
              "id": "referral-link-1",
              "user_id": "user-1",
              "referral_program_id": "topic-1",
              "referral_program_name": "Credit Card",
              "referral_program_slug": "credit-card",
              "url": "https://example.com/referral",
              "label": "Native card"
            }
          ],
          "users": {
            "user-1": { "id": "user-1", "username": "alice", "display_name": null, "profile_image_id": null }
          },
          "page_info": { "has_next_page": false, "end_cursor": null, "start_cursor": null }
        }
        """.utf8)
    }
}
