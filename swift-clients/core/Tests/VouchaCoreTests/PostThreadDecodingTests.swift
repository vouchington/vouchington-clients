import Foundation
@testable import VouchaModels
import XCTest

final class PostThreadDecodingTests: XCTestCase {
    private let decoder = makeVouchaDecoder()

    func testDecodesNativeCommentThreadFixtures() throws {
        let detail = try decoder.decode(PostEnvelope.self, from: fixture("native.comments.post-detail.default"))
        XCTAssertEqual(detail.post.id, "comment-root-post")
        XCTAssertEqual(detail.post.createdBy?.username, "testuser")
        XCTAssertEqual(detail.post.canLock, true)
        XCTAssertEqual(detail.postMetrics?.count.children, 2)
        XCTAssertEqual(detail.bookmarks?[detail.post.id]?["save"], true)

        let descendants = try decoder.decode(
            PostThreadEnvelope.self,
            from: fixture("native.comments.descendants.default")
        )
        XCTAssertEqual(descendants.results.map(\.entityId), ["comment-a", "comment-b", "comment-c", "comment-d"])
        XCTAssertEqual(descendants.posts["comment-a"]?.canEditContent, true)
        XCTAssertEqual(descendants.posts["comment-b"]?.createdBy?.username, "commenter")
        XCTAssertEqual(descendants.posts["comment-c"]?.isAnonymous, true)
        XCTAssertNotNil(descendants.posts["comment-d"]?.deletedAt)
        XCTAssertEqual(descendants.postsMetrics?["comment-b"]?.count.ancestors, 2)
        XCTAssertEqual(descendants.postElections?["comment-a"]?.votesScoreNet, 8)
        XCTAssertEqual(descendants.electionVotes?["comment-b"]?.choice, .dislike)
        XCTAssertEqual(descendants.markdownToHtml?["comment-a"], "<p>Top-level native comment</p>")
        XCTAssertEqual(descendants.bookmarks?["comment-a"]?["save"], true)
    }

    private func fixture(_ id: String) -> Data {
        ApiFixtureLoader.data(id)
    }
}
