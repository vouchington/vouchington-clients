import Foundation
import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class NativeCommentThreadListRetentionTests: NativeRouteSurfaceViewModelTestCase {
    func testCommentHideRemainsInFeedOnReadAndBothMutationDirections() async throws {
        let id = CommentFixture.a
        let path = "/api/v1/bookmarks/post/\(id)/hide"
        CannedFeedURLProtocol.handlers["/api/v1/feeds/posts/any"] = try (threadPage(ids: [id], hidden: true), 200)
        CannedFeedURLProtocol.handlers[path] = (Data("{}".utf8), 200)
        let model = try PostsListViewModel(client: makeClient())
        await model.load()
        XCTAssertEqual(model.items.map(\.id), [id])
        XCTAssertTrue(model.isHidden(postId: id))
        await model.toggleHide(postId: id)
        XCTAssertFalse(model.isHidden(postId: id))
        await model.toggleHide(postId: id)
        XCTAssertTrue(model.isHidden(postId: id))
        XCTAssertEqual(model.items.map(\.id), [id])
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedRequests.filter { $0.url.path == path }.map(\.method),
            ["DELETE", "PUT"]
        )
    }

    func testHiddenPostsScreenKeepsCommentRows() async throws {
        let fixture = try object("native.comments.descendants.default")
        let posts = try XCTUnwrap(fixture["posts"] as? [String: [String: Any]])
        let comment = try XCTUnwrap(posts[CommentFixture.a])
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (PrivateUserTestFixture.identityEnvelope(), 200)
        CannedFeedURLProtocol.handlers["/api/v1/users/user-1/posts/hidden"] = try (
            JSONSerialization.data(withJSONObject: ["results": [comment], "page_info": ["has_next_page": false]]), 200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/posts/hidden"))
        let model = try NativeRouteSurfaceViewModel(entry: route.entry, client: makeClient(), routeMatch: route.match)
        await model.load()
        XCTAssertEqual(model.bookmarkRows.map(\.entityId), [CommentFixture.a])
        XCTAssertEqual(
            model.bookmarkRows.first?.destination,
            .comment(id: CommentFixture.a, rootId: CommentFixture.root)
        )
    }

    func testDeleteReloadsPlaceholderConnectorAndPreservesItsReply() async throws {
        let root = CommentFixture.root
        let descendantsPath = "/api/v1/posts/\(root)/descendants"
        CannedFeedURLProtocol.handlers["/api/v1/posts/\(root)"] = (
            ApiFixtureLoader.data("native.comments.post-detail.default"),
            200
        )
        var deletedPage = try JSONSerialization.jsonObject(with: threadPage(ids: [
            CommentFixture.a,
            CommentFixture.b
        ])) as? [String: Any]
        var posts = try XCTUnwrap(deletedPage?["posts"] as? [String: [String: Any]])
        posts[CommentFixture.a]?["placeholder_state"] = "deleted"
        posts[CommentFixture.a]?["deleted_at"] = "2026-01-02T00:00:00Z"
        posts[CommentFixture.a]?["markdown"] = ""
        posts[CommentFixture.a]?["created_by_id"] = NSNull()
        posts[CommentFixture.a]?["created_by"] = NSNull()
        deletedPage?["posts"] = posts
        CannedFeedURLProtocol.queuedHandlers[descendantsPath] = try [
            (threadPage(ids: [CommentFixture.a, CommentFixture.b]), 200, 0),
            (JSONSerialization.data(withJSONObject: XCTUnwrap(deletedPage)), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/posts/\(CommentFixture.a)"] = (Data("{}".utf8), 200)
        let model = try NativeCommentThreadViewModel(client: makeClient(), rootPostId: root, currentUserId: "user-1")
        await model.loadThread()
        await model.delete(postId: CommentFixture.a)
        XCTAssertEqual(model.commentTree.map(\.id), [CommentFixture.a])
        XCTAssertEqual(model.commentTree.first?.post.placeholderState, "deleted")
        XCTAssertEqual(model.commentTree.first?.children.map(\.id), [CommentFixture.b])
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(descendantsPath), 2)
    }

    private func threadPage(ids: [String], hidden: Bool = false) throws -> Data {
        var fixture = try object("native.comments.descendants.default")
        fixture["results"] = ids.map { ["entity_id": $0] }
        fixture["page_info"] = ["has_next_page": false]
        fixture["bookmarks"] = [CommentFixture.a: ["hide": hidden]]
        return try JSONSerialization.data(withJSONObject: fixture)
    }

    private func object(_ id: String) throws -> [String: Any] {
        try XCTUnwrap(JSONSerialization.jsonObject(with: ApiFixtureLoader.data(id)) as? [String: Any])
    }
}
