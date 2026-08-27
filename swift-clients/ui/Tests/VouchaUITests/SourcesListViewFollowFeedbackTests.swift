import Foundation
import ViewInspector
@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class SourcesListViewFollowFeedbackTests: XCTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
    }

    private func makeViewModel(userId: String? = "user-abc", feedType: String? = nil) -> SourcesListViewModel {
        let config = AppConfig(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test-site-key")
        let apiClient = APIClient(
            config: config,
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        return SourcesListViewModel(client: apiClient, userId: userId, feedType: feedType)
    }

    private func makeSourcesPage(ids: [String], bookmarks: [String: [String: Bool]]? = nil) -> Data {
        let items = ids.map { id in
            """
            {
              "id":"\(id)",
              "title":"Source \(id)",
              "is_enabled":true,
              "is_discoverable":true,
              "etag":null,
              "last_modified_at":null,
              "last_fetched_at":null,
              "feed_type":"article",
              "rss_feed_url":{"url":"https://example.com/\(id).xml","canonical_url_id":null},
              "home_page_url":null,
              "hostname":{"hostname":"example\(id).com","id":"h\(id)"},
              "topic":{"id":"t1","slug":"tech","topic_type":"topic","name":"Tech","hostname_id":null,"hostname":null,"logo_image_id":null,"hero_image_id":null,"homepage_url_id":null,"lingua_rs_detected_language":null,"referral_program_id":null,"referral_program_slug":null,"rewards_program_id":null},
              "publisher_type":null,
              "podcast_show":null
            }
            """
        }.joined(separator: ",")
        let bookmarksJSON = bookmarks.map { bookmarks in
            let entries = bookmarks.map { sourceId, predicates in
                let flags = predicates.map { "\"\($0.key)\":\($0.value)" }.joined(separator: ",")
                return "\"\(sourceId)\":{\(flags)}"
            }.joined(separator: ",")
            return ",\"bookmarks\":{\(entries)}"
        } ?? ""
        return Data("""
        {"results":[\(items)],"topic_elections":{},"election_votes":{}\(bookmarksJSON)}
        """.utf8)
    }

    func testToggleUnfollowUsesDeleteRemovesRowAndSetsStatusMessage() async {
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/rss-feeds/following"] = (
            makeSourcesPage(ids: ["s1", "s2"]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/rss_feed/s1/follow"] = (Data("{}".utf8), 204)
        let viewModel = makeViewModel()
        await viewModel.load()

        await viewModel.toggleFollow(sourceId: "s1")

        XCTAssertEqual(viewModel.items.map(\.id), ["s2"])
        XCTAssertFalse(viewModel.isFollowing(sourceId: "s1"))
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.path, "/api/v1/bookmarks/rss_feed/s1/follow")
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.last, "DELETE")
        XCTAssertEqual(viewModel.statusMessage, .message(.nativeSwiftSourcesSourceUnfollowed))
    }

    func testToggleUnfollowRollsBackOnFailure() async {
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/rss-feeds/following"] = (
            makeSourcesPage(ids: ["s1", "s2"]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/rss_feed/s1/follow"] = (Data("{}".utf8), 500)
        let viewModel = makeViewModel()
        await viewModel.load()

        await viewModel.toggleFollow(sourceId: "s1")

        XCTAssertEqual(viewModel.items.map(\.id), ["s1", "s2"])
        XCTAssertTrue(viewModel.isFollowing(sourceId: "s1"))
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.path, "/api/v1/bookmarks/rss_feed/s1/follow")
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.last, "DELETE")
        XCTAssertEqual(viewModel.statusMessage, .message(.nativeSwiftSourcesUnableToUnfollowSource))
    }

    func testReloadClearsStatusMessage() async {
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/rss-feeds/following"] = (
            makeSourcesPage(ids: ["s1"]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/rss_feed/s1/follow"] = (Data("{}".utf8), 204)
        let viewModel = makeViewModel()
        await viewModel.load()
        await viewModel.toggleFollow(sourceId: "s1")
        XCTAssertEqual(viewModel.statusMessage, .message(.nativeSwiftSourcesSourceUnfollowed))

        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/rss-feeds/following"] = (
            makeSourcesPage(ids: ["s2"]),
            200
        )
        await viewModel.reload()

        XCTAssertNil(viewModel.statusMessage)
    }

    func testToggleUnfollowRollbackUsesNeighborPosition() async {
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/rss-feeds/following"] = (
            makeSourcesPage(ids: ["s1", "s2", "s3"]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/rss_feed/s2/follow"] = (Data("{}".utf8), 500)
        let viewModel = makeViewModel()
        await viewModel.load()

        await viewModel.toggleFollow(sourceId: "s2")

        XCTAssertEqual(viewModel.items.map(\.id), ["s1", "s2", "s3"])
        XCTAssertEqual(viewModel.statusMessage, .message(.nativeSwiftSourcesUnableToUnfollowSource))
    }

    func testSourcesListViewRendersStatusBannerAfterUnfollow() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/rss-feeds/following"] = (
            makeSourcesPage(ids: ["s1"]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/rss_feed/s1/follow"] = (Data("{}".utf8), 204)
        let viewModel = makeViewModel()
        await viewModel.load()
        await viewModel.toggleFollow(sourceId: "s1")

        let sut = SourcesListView(viewModel: viewModel, scope: .your, navigationTitle: "Your Sources")

        XCTAssertEqual(try sut.inspect().find(text: "Source unfollowed").string(), "Source unfollowed")
        XCTAssertEqual(try sut.inspect().find(text: "No Sources").string(), "No Sources")
    }
}
