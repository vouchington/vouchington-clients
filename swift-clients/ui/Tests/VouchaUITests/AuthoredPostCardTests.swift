import Foundation
import ViewInspector
import VouchaDesignSystem
import VouchaModels
import XCTest

@MainActor
final class AuthoredPostCardTests: XCTestCase {
    func testRejectedPostCardUsesViewerSpecificNoticeWithoutExposingBody() throws {
        let post = makePost(markdown: "Private rejected body", clearanceStatus: .rejected)
        var appealOpened = false
        let author = PostCard(post: post, currentUserId: "user-1", onViewAppeals: { appealOpened = true })
        let other = PostCard(post: post, currentUserId: "other")
        let staff = PostCard(post: post, currentUserId: "other", isStaffViewer: true)

        XCTAssertNoThrow(try author.inspect().find(text: "This content was removed by a moderator."))
        XCTAssertThrowsError(try author.inspect().find(text: "Private rejected body"))
        try author.inspect().find(button: "View appeals").tap()
        XCTAssertTrue(appealOpened)
        XCTAssertNoThrow(try other.inspect().find(text: "This content is unavailable."))
        XCTAssertThrowsError(try other.inspect().find(text: "Private rejected body"))
        XCTAssertThrowsError(try other.inspect().find(button: "View appeals"))
        XCTAssertNoThrow(try staff.inspect().find(text: "In review"))
        XCTAssertNoThrow(try staff.inspect().find(text: "Private rejected body"))
    }

    func testNormalTitledCompactPostStillHidesBodyAfterClearanceRecovery() throws {
        let card = PostCard(post: makePost(markdown: "Body", clearanceStatus: .approved), currentUserId: "user-1")

        XCTAssertNoThrow(try card.inspect().find(text: "Discussion"))
        XCTAssertThrowsError(try card.inspect().find(text: "Body"))
        XCTAssertThrowsError(try card.inspect().find(text: "This content was removed by a moderator."))
    }

    func testAuthorNoticeRendersPublicReasonCodeOrLocalizedFallback() throws {
        var post = makePost(markdown: "Private body", clearanceStatus: .rejected)
        post.clearanceReason = "off_topic"
        XCTAssertNoThrow(try PostCard(post: post, currentUserId: "user-1")
            .inspect().find(text: "off_topic"))

        post.clearanceReason = nil
        XCTAssertNoThrow(try PostCard(post: post, currentUserId: "user-1")
            .inspect().find(text: "No additional reason was provided."))
    }

    private func makePost(markdown: String, clearanceStatus: ClearanceStatus) -> Post {
        Post(
            id: "post-1", slug: "post-1", postType: .discussion,
            title: "Discussion", markdown: markdown, html: nil,
            parentId: nil, rootId: nil, createdById: "user-1",
            createdAt: Date(timeIntervalSince1970: 0), broadcast: nil,
            privacy: .public, isAnonymous: false, communityId: nil,
            clearanceStatus: clearanceStatus
        )
    }
}
