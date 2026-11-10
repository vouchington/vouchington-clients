import Foundation
@testable import VouchaModels
import XCTest

final class PostThreadDecodingTests: XCTestCase {
    private let decoder = makeVouchaDecoder()
    private let rootId = "70000000-0000-7000-8000-000000005400"
    private let commentA = "70000000-0000-7000-8000-000000005401"
    private let commentB = "70000000-0000-7000-8000-000000005402"
    private let commentC = "70000000-0000-7000-8000-000000005403"

    func testDecodesNativeCommentThreadFixtures() throws {
        let detail = try decoder.decode(PostEnvelope.self, from: fixture("native.comments.post-detail.default"))
        XCTAssertEqual(detail.post.id, rootId)
        XCTAssertEqual(detail.post.createdBy?.username, "testuser")
        XCTAssertEqual(detail.post.canLock, true)
        XCTAssertEqual(detail.postMetrics?.count.children, 2)
        XCTAssertEqual(detail.bookmarks?[detail.post.id]?["save"], true)

        let descendants = try decoder.decode(
            PostThreadEnvelope.self,
            from: fixture("native.comments.descendants.default")
        )
        XCTAssertEqual(descendants.results.map(\.entityId), [commentA, commentB, commentC])
        XCTAssertEqual(descendants.posts[commentA]?.canEditContent, true)
        XCTAssertEqual(descendants.posts[commentB]?.createdBy?.username, "commenter")
        XCTAssertEqual(descendants.posts[commentC]?.isAnonymous, true)
        XCTAssertEqual(descendants.postElections?[commentA]?.votesScoreNet, 8)
        XCTAssertEqual(descendants.electionVotes?[commentB]?.choice, .dislike)
        XCTAssertEqual(descendants.markdownToHtml?[commentA], "<p>Top-level native comment</p>")
        XCTAssertEqual(descendants.bookmarks?[commentA]?["save"], true)
    }

    func testTombstonedFixtureCommentBecomesAnUnavailablePlaceholder() throws {
        var object = try XCTUnwrap(
            JSONSerialization.jsonObject(with: fixture("native.comments.descendants.default")) as? [String: Any]
        )
        var posts = try XCTUnwrap(object["posts"] as? [String: Any])
        var nested = try XCTUnwrap(posts[commentB] as? [String: Any])
        nested["placeholder_state"] = "deleted"
        nested["deleted_at"] = "2026-01-02T00:00:00Z"
        posts[commentB] = nested
        object["posts"] = posts

        let descendants = try decoder.decode(
            PostThreadEnvelope.self,
            from: JSONSerialization.data(withJSONObject: object)
        )
        let deleted = try XCTUnwrap(descendants.posts[commentB])
        XCTAssertNotNil(deleted.deletedAt)
        XCTAssertEqual(deleted.placeholderState, "deleted")
        XCTAssertTrue(deleted.isCommentPlaceholder)
        XCTAssertEqual(deleted.contentPresentation(viewerId: nil), .unavailable)
    }

    func testDecodesHackerNewsReferences() throws {
        let detail = try decoder.decode(PostEnvelope.self, from: fixture("native.posts.hacker-news.detail"))
        let references = try XCTUnwrap(detail.postHackerNewsReferences?[rootId])
        XCTAssertEqual(references.commentCount, 2)
        XCTAssertEqual(references.storyCount, 1)
        XCTAssertEqual(references.stories?.first?.points, 12)
        XCTAssertEqual(references.comments?.map(\.hackerNewsItemId), [303, 302])
    }

    private func fixture(_ id: String) -> Data {
        ApiFixtureLoader.data(id)
    }
}
