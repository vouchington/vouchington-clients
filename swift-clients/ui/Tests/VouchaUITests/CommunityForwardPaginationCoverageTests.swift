import Foundation
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class CommunityForwardPaginationCoverageTests: NativeRouteSurfaceViewModelTestCase {
    func testPendingReportsFailurePreservesPageThenRetryAppendsAndUpdatesCount() async throws {
        let path = "/api/v1/communities/builders/reports/pending"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (Data(#"{"message":"offline"}"#.utf8), 503, 0),
            (pendingReportsPage(ids: ["report-2"], cursor: nil, hasMore: false), 200, 0)
        ]
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .moderation
        )
        let reports = try decodeReports(ids: ["report-1"])
        viewModel.pendingReportPagination.reset(items: reports)
        viewModel.pendingReportPagination.restoreContinuation(endCursor: "reports-cursor", hasMore: true)
        viewModel.summary.rows = [
            .init(
                icon: "exclamationmark.triangle",
                title: UiMessage(.nativeSwiftCommunitiesPendingReports),
                detail: UiMessage(.nativeSwiftCommunitiesQueuedCount, numberParameters: ["count": 1])
            )
        ]

        await viewModel.loadMorePendingReports()

        XCTAssertEqual(viewModel.pendingReportPagination.items.map(\.id), ["report-1"])
        XCTAssertNotNil(viewModel.pendingReportPagination.lastError)

        await viewModel.loadMorePendingReports()

        XCTAssertEqual(viewModel.pendingReportPagination.items.map(\.id), ["report-1", "report-2"])
        XCTAssertNil(viewModel.pendingReportPagination.lastError)
        XCTAssertEqual(viewModel.summary.rows[0].detail, "2 queued")
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.suffix(2).map(\.query), [
            "limit=50&sort=created_at_desc&after=reports-cursor",
            "limit=50&sort=created_at_desc&after=reports-cursor"
        ])
    }

    func testModmailThreadFailurePreservesPageThenRetryAppendsAndDeduplicates() async throws {
        let path = "/api/v1/communities/builders/modmail"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (Data(#"{"message":"offline"}"#.utf8), 503, 0),
            (modmailPage(ids: ["thread-1", "thread-2"], cursor: nil, hasMore: false), 200, 0)
        ]
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .modmail
        )
        let threads = try decodeThreads(ids: ["thread-1"])
        viewModel.modmailThreadPagination.reset(items: threads)
        viewModel.modmailThreadPagination.restoreContinuation(endCursor: "threads-cursor", hasMore: true)
        viewModel.modmailRowIds = ["thread-1"]
        viewModel.summary.rows = [threadRow(id: "thread-1")]

        XCTAssertTrue(viewModel.canLoadMoreModmail)
        await viewModel.loadMoreModmail()

        XCTAssertEqual(viewModel.modmailThreadPagination.items.map(\.id), ["thread-1"])
        XCTAssertNotNil(viewModel.modmailPaginationError)
        XCTAssertTrue(viewModel.canLoadMoreModmail)

        await viewModel.loadMoreModmail()

        XCTAssertEqual(viewModel.modmailThreadPagination.items.map(\.id), ["thread-1", "thread-2"])
        XCTAssertEqual(viewModel.summary.rows.count, 2)
        XCTAssertNil(viewModel.modmailPaginationError)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.suffix(2).map(\.query), [
            "after=threads-cursor&limit=25",
            "after=threads-cursor&limit=25"
        ])
    }

    func testForwardRowsFailurePreservesPageThenRetryAppends() async throws {
        let path = "/api/v1/communities/builders/posts"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (Data(#"{"message":"offline"}"#.utf8), 503, 0),
            (postPage(id: "post-2"), 200, 0)
        ]
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .posts
        )
        viewModel.rowPagination.reset(items: [
            .init(id: "post-1", row: .init(icon: "doc.text", title: .verbatim("One"), detail: .verbatim("First")))
        ])
        viewModel.rowPagination.restoreContinuation(endCursor: "posts-cursor", hasMore: true)
        viewModel.summary.rows = viewModel.rowPagination.items.map(\.row)

        await viewModel.loadMoreRows()
        XCTAssertEqual(viewModel.summary.rows.map(\.title), ["One"])
        XCTAssertNotNil(viewModel.rowPagination.lastError)

        await viewModel.loadMoreRows()

        XCTAssertEqual(viewModel.summary.rows.map(\.title), ["One", "Two"])
        XCTAssertNil(viewModel.rowPagination.lastError)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.suffix(2).map(\.query), [
            "limit=10&after=posts-cursor",
            "limit=10&after=posts-cursor"
        ])
    }

    func testInitialPostReloadClearsEmbedsOmittedByAcceptedResponse() async throws {
        let path = "/api/v1/communities/builders/posts"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (postPage(id: "post-1", cursor: nil, hasMore: false, embedTitle: "First preview"), 200, 0),
            (postPage(id: "post-1", cursor: nil, hasMore: false), 200, 0)
        ]
        let client = try makeClient()
        let viewModel = CommunityDetailViewModel(client: client, slug: "builders")

        _ = try await viewModel.loadInitialRows(client: client, tab: .posts, revision: 0)
        XCTAssertEqual(viewModel.postEmbedsByPostId["post-1"]?.previewTitle, "First preview")

        viewModel.rowPagination.reset()
        _ = try await viewModel.loadInitialRows(client: client, tab: .posts, revision: 0)

        XCTAssertTrue(viewModel.postEmbedsByPostId.isEmpty)
    }

    func testPostPaginationRetainsInitialEmbedsWhileMergingAcceptedNextPage() async throws {
        let path = "/api/v1/communities/builders/posts"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (postPage(id: "post-1", cursor: "next", hasMore: true, embedTitle: "First preview"), 200, 0),
            (postPage(id: "post-2", cursor: nil, hasMore: false, embedTitle: "Second preview"), 200, 0)
        ]
        let client = try makeClient()
        let viewModel = CommunityDetailViewModel(client: client, slug: "builders")

        _ = try await viewModel.loadInitialRows(client: client, tab: .posts, revision: 0)
        await viewModel.loadMoreRows()

        XCTAssertEqual(viewModel.postEmbedsByPostId["post-1"]?.previewTitle, "First preview")
        XCTAssertEqual(viewModel.postEmbedsByPostId["post-2"]?.previewTitle, "Second preview")
    }

    func testStaleNewsResponseCannotReplaceAcceptedNewsEmbedSidecars() async throws {
        let path = "/api/v1/communities/builders/news"
        CannedFeedURLProtocol.handlers[path] = (newsPage(id: "news-1", embedTitle: "Stale preview"), 200)
        CannedFeedURLProtocol.suspendResponse(path: path)
        let client = try makeClient()
        let viewModel = CommunityDetailViewModel(client: client, slug: "builders", initialTab: .news)
        let currentEmbed = try embed(title: "Current preview")
        viewModel.rssFeedItemEmbedsById = ["current": currentEmbed]

        let staleLoad = Task {
            try await viewModel.loadInitialRows(client: client, tab: .news, revision: viewModel.communityLoadRevision)
        }
        await waitForSuspendedResponse(path: path)
        viewModel.selectedTab = .posts
        CannedFeedURLProtocol.releaseResponse(path: path)
        _ = try? await staleLoad.value

        XCTAssertEqual(viewModel.rssFeedItemEmbedsById["current"]?.previewTitle, "Current preview")
        XCTAssertNil(viewModel.rssFeedItemEmbedsById["news-1"])
    }

    func testAcceptedInitialNewsLoadReplacesEmbedsOmittedByNextPage() async throws {
        let path = "/api/v1/communities/builders/news"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (newsPage(id: "news-1", embedTitle: "First preview"), 200, 0),
            (newsPage(id: "news-1"), 200, 0)
        ]
        let client = try makeClient()
        let viewModel = CommunityDetailViewModel(client: client, slug: "builders", initialTab: .news)

        _ = try await viewModel.loadInitialRows(client: client, tab: .news, revision: 0)
        XCTAssertEqual(viewModel.rssFeedItemEmbedsById["news-1"]?.previewTitle, "First preview")

        viewModel.rowPagination.reset()
        _ = try await viewModel.loadInitialRows(client: client, tab: .news, revision: 0)

        XCTAssertTrue(viewModel.rssFeedItemEmbedsById.isEmpty)
    }

    func testNewsPaginationRetainsInitialEmbedsWhileMergingAcceptedNextPage() async throws {
        let path = "/api/v1/communities/builders/news"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (newsPage(id: "news-1", cursor: "next", hasMore: true, embedTitle: "First preview"), 200, 0),
            (newsPage(id: "news-2", embedTitle: "Second preview"), 200, 0)
        ]
        let client = try makeClient()
        let viewModel = CommunityDetailViewModel(client: client, slug: "builders", initialTab: .news)

        _ = try await viewModel.loadInitialRows(client: client, tab: .news, revision: 0)
        await viewModel.loadMoreRows()

        XCTAssertEqual(viewModel.rssFeedItemEmbedsById["news-1"]?.previewTitle, "First preview")
        XCTAssertEqual(viewModel.rssFeedItemEmbedsById["news-2"]?.previewTitle, "Second preview")
    }

    private func decodeReports(ids: [String]) throws -> [CommunityPendingReport] {
        try decoder.decode([CommunityPendingReport].self, from: Data(
            "[\(ids.map(pendingReport).joined(separator: ","))]".utf8
        ))
    }

    private func decodeThreads(ids: [String]) throws -> [CommunityModmailThread] {
        try decoder.decode(CommunityModmailThreadsResponse.self, from: modmailPage(
            ids: ids,
            cursor: nil,
            hasMore: false
        )).results
    }

    private func pendingReportsPage(ids: [String], cursor: String?, hasMore: Bool) -> Data {
        let results = ids.map(pendingReport).joined(separator: ",")
        return Data(
            #"{"reports":[\#(results)],"page_info":{"has_next_page":\#(hasMore),"end_cursor":\#(json(cursor))}}"#
                .utf8
        )
    }

    private func pendingReport(id: String) -> String {
        #"{"id":"\#(id)","case_id":"case-\#(id)","entity_type":"post","entity_id":"post-\#(id)","target_label":"\#(id)","target_path":null,"target_content":null,"reason":"spam","status":"pending","report_count":1,"created_at":"2026-01-01T00:00:00Z","reviewed_at":null,"target_user_id":null,"reporter_user_id":null,"reporter_username":null,"note":null,"resolved_by_id":null,"admin_action_path":null,"target_available":true,"judgement":null,"community_ban_evasion":null,"claim":null,"escalated_at":null,"escalated_by_id":null}"#
    }

    private func modmailPage(ids: [String], cursor: String?, hasMore: Bool) -> Data {
        let results = ids.map { id in
            #"{"id":"\#(id)","channel_type":"modmail","title":"\#(id)","community_id":"community-1","subject_user_id":null,"assigned_moderator_user_id":null,"assigned_at":null,"resolved_at":null,"resolved_by_id":null,"created_by_id":"user-1","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z"}"#
        }.joined(separator: ",")
        return Data(
            #"{"results":[\#(results)],"page_info":{"has_next_page":\#(hasMore),"end_cursor":\#(json(cursor))}}"#
                .utf8
        )
    }

    private func postPage(
        id: String,
        cursor: String? = nil,
        hasMore: Bool = false,
        embedTitle: String? = nil
    ) -> Data {
        let postLinkEmbeds = embedTitle.map {
            #","post_link_embeds":{"\#(id)":{"source_url":"https://example.com/\#(id)","title":"\#($0)"}}"#
        } ?? ""
        return Data(
            #"{"results":[{"id":"\#(id)"}],"page_info":{"has_next_page":\#(hasMore),"end_cursor":\#(json(cursor))},"posts":{"\#(id)":{"id":"\#(id)","post_type":"discussion","title":"Two","slug":"two","markdown":"Body","created_by_id":"user-1","created_at":"2026-01-01T00:00:00Z"}}\#(postLinkEmbeds),"posts_metrics":{},"communities":{}}"#
                .utf8
        )
    }

    private func newsPage(
        id: String,
        cursor: String? = nil,
        hasMore: Bool = false,
        embedTitle: String? = nil
    ) -> Data {
        let rssFeedItemEmbeds = embedTitle.map {
            #","rss_feed_item_embeds":{"\#(id)":{"source_url":"https://example.com/\#(id)","title":"\#($0)"}}"#
        } ?? ""
        return Data(
            #"{"results":[{"id":"\#(id)","entity_id":"\#(id)"}],"page_info":{"has_next_page":\#(hasMore),"end_cursor":\#(json(cursor))},"rss_feed_items":{"\#(id)":{"id":"\#(id)","title":"News item"}}\#(rssFeedItemEmbeds)}"#
                .utf8
        )
    }

    private func embed(title: String) throws -> UrlEmbed {
        try decoder.decode(
            UrlEmbed.self,
            from: Data(#"{"source_url":"https://example.com/current","title":"\#(title)"}"#.utf8)
        )
    }

    private func threadRow(id: String) -> NativeRouteDestinationRow {
        .init(icon: "bubble.left.and.bubble.right", title: .verbatim(id), detail: .verbatim("Open"))
    }

    private func json(_ value: String?) -> String {
        value.map { #""\#($0)""# } ?? "null"
    }

    private func waitForSuspendedResponse(path: String) async {
        for _ in 0 ..< 200 where !CannedFeedURLProtocol.hasSuspendedResponse(path: path) {
            try? await Task.sleep(for: .milliseconds(1))
        }
        XCTAssertTrue(CannedFeedURLProtocol.hasSuspendedResponse(path: path))
    }

    private var decoder: JSONDecoder {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return decoder
    }
}
