import Foundation
import ViewInspector
@testable import VouchaAPI
@testable import VouchaAuth
@testable import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class RSSFeedListViewPlaybackAccessoryGateTests: XCTestCase {
    private let apiBaseURL = URL(string: "http://localhost:2999")!

    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
    }

    private func makeClient() -> APIClient {
        APIClient(
            config: AppConfig(baseURL: apiBaseURL, turnstileSiteKey: "test-site-key"),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
    }

    private func makePlaybackController() -> PodcastPlaybackController {
        let client = makeClient()
        return PodcastPlaybackController(
            client: client,
            sessionManager: SessionManager(client: client, cookieStorage: HTTPCookieStorage())
        )
    }

    private func makeFeedPage(mediaType: String) -> Data {
        Data("""
        {
          "results":[{"id":"item-1","entity_id":"item-1"}],
          "page_info":{"has_next_page":false,"end_cursor":null},
          "rss_feed_items":{
            "item-1":{
              "id":"item-1",
              "rss_feed_id":"feed-1",
              "title":"Item item-1",
              "description":"Summary",
              "content":null,
              "link":"https://example.com/item-1",
              "published_at":"2026-01-01T00:00:00Z",
              "creator":"Author",
              "categories":[],
              "media_content":{
                "url":"https://example.com/item-1.\(mediaType == "audio" ? "mp3" : "mp4")",
                "type":"\(mediaType == "audio" ? "audio/mpeg" : "video/mp4")",
                "medium":"\(mediaType)",
                "duration":600
              }
            }
          },
          "rss_feed_item_thumbnail_url":{},
          "rss_feed_item_elections":{},
          "election_votes":{}
        }
        """.utf8)
    }

    private func makeViewModel(contentType: ContentType) async -> RSSFeedListViewModel {
        let mediaType = switch contentType {
        case .news, .podcast:
            "audio"
        case .video:
            "video"
        }
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(mediaType: mediaType),
            200
        )
        let vm = RSSFeedListViewModel(client: makeClient(), contentType: contentType)
        await vm.load()
        return vm
    }

    func testNewsFeedHidesPlaybackAccessoryForPlayableMediaItems() async throws {
        let viewModel = await makeViewModel(contentType: .news)
        let sut = RSSFeedListView(
            viewModel: viewModel,
            playbackController: makePlaybackController()
        )

        XCTAssertThrowsError(try sut.inspect().find(button: "Play"))
    }

    func testPodcastFeedShowsPlaybackAccessoryForPlayableMediaItems() async throws {
        let viewModel = await makeViewModel(contentType: .podcast)
        let sut = RSSFeedListView(
            viewModel: viewModel,
            playbackController: makePlaybackController()
        )

        XCTAssertNoThrow(try sut.inspect().find(button: "Play"))
    }

    func testApprovedEmbedSuppressesUnavailableAccessoryAndShowsProviderActions() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (Data(#"""
        { "results": [{ "id": "item-1", "entity_id": "item-1" }],
          "page_info": { "has_next_page": false, "end_cursor": null },
          "rss_feed_items": { "item-1": { "id": "item-1", "rss_feed_id": "feed-1", "title": "Embed video", "link": "https://example.com/item-1", "media_type": "video", "video_id": "video-1", "video_platform": "youtube" } },
          "rss_feed_item_embeds": { "item-1": { "source_url": "https://example.com/item-1", "player_url": "https://www.youtube-nocookie.com/embed/video-1" } } }
        """#.utf8), 200)
        let viewModel = RSSFeedListViewModel(client: makeClient(), contentType: .video)
        await viewModel.load()
        let sut = RSSFeedListView(viewModel: viewModel, playbackController: makePlaybackController())

        XCTAssertNoThrow(try sut.inspect().find(button: "Play video"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Open source"))
        XCTAssertThrowsError(try sut.inspect().find(text: "Video unavailable"))
    }
}
