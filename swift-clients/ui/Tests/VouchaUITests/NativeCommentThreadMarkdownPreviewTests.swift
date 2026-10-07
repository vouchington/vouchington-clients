import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeCommentThreadMarkdownPreviewTests: XCTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
    }

    override func tearDown() {
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
        super.tearDown()
    }

    func testReplyPreviewsMarkdownWhenMutationOmitsHtml() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/markdown/preview"] = (
            Data(#"{"html":"<p><strong>Fresh reply</strong></p>"}"#.utf8),
            200
        )
        CannedFeedURLProtocol.queuedHandlers["/api/v1/posts"] = [(replyMutationWithoutHtmlJSON, 200, 0)]

        let viewModel = try makeViewModel()
        await viewModel.reply(parentId: "comment-a", markdown: "**Fresh reply**")

        let reply = try XCTUnwrap(
            viewModel.descendantPosts.first { $0.id == "comment-c" }
        )
        XCTAssertEqual(reply.markdown, "**Fresh reply**")
        XCTAssertEqual(reply.html, "<p><strong>Fresh reply</strong></p>")
    }

    func testReplyKeepsCommittedPostWhenPreviewFails() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/markdown/preview"] = (Data(#"{"error":"failed"}"#.utf8), 500)
        CannedFeedURLProtocol.queuedHandlers["/api/v1/posts"] = [(replyMutationWithoutHtmlJSON, 200, 0)]

        let viewModel = try makeViewModel()
        await viewModel.reply(parentId: "comment-a", markdown: "**Fresh reply**")

        let reply = try XCTUnwrap(
            viewModel.descendantPosts.first { $0.id == "comment-c" }
        )
        XCTAssertEqual(reply.markdown, "**Fresh reply**")
        XCTAssertNil(reply.html)
    }

    private func makeViewModel() throws -> NativeCommentThreadViewModel {
        try NativeCommentThreadViewModel(
            client: APIClient(
                config: AppConfig(baseURL: XCTUnwrap(URL(string: "http://localhost:2999"))),
                cookieStorage: HTTPCookieStorage(),
                protocolClasses: [CannedFeedURLProtocol.self]
            ),
            rootPostId: "root-1",
            currentUserId: "user-1"
        )
    }

    private var replyMutationWithoutHtmlJSON: Data {
        Data("""
        {
          "post": {
            "id": "comment-c",
            "slug": null,
            "post_type": "comment",
            "title": null,
            "markdown": "**Fresh reply**",
            "parent_post_id": "comment-a",
            "root_post_id": "root-1",
            "created_by_id": "user-1",
            "created_at": "2026-01-01T00:02:00Z",
            "broadcast": "everyone",
            "privacy": "public",
            "is_anonymous": false,
            "community_id": null,
            "clearance_status": null,
            "deleted_at": null,
            "deleted_by_id": null,
            "locked_at": null,
            "locked_by_id": null,
            "archived_at": null,
            "archived_by_id": null,
            "clearance_reason": null,
            "clearance_updated_at": null,
            "spam_detection_created_at": null,
            "spam_detection_flagged": null,
            "spam_detection_results": null,
            "spam_detection_score": null,
            "updated_by_id": null
          }
        }
        """.utf8)
    }
}
