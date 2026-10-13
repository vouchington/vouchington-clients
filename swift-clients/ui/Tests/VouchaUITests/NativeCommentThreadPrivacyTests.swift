import Foundation
import ViewInspector
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class NativeCommentThreadPrivacyTests: XCTestCase {
    func testRejectedThreadCannotQuoteHiddenBodyOrSearchItsUrls() throws {
        let post = Post(
            id: "root-1",
            slug: nil,
            postType: .discussion,
            title: nil,
            markdown: "Private https://example.com/hidden-story",
            html: nil,
            parentId: nil,
            rootId: "root-1",
            createdById: "user-1",
            createdAt: Date(),
            broadcast: .everyone,
            privacy: .public,
            isAnonymous: false,
            communityId: nil,
            clearanceStatus: .rejected
        )
        let section = CommentThreadPostSection(
            title: .verbatim("Removed discussion"),
            post: post,
            embed: nil,
            pathText: "/discussion/root-1",
            voteChoice: nil,
            isSignedIn: true,
            canCreateVote: true,
            showSignIn: {},
            hideDownCount: false,
            isRoot: true,
            isCollapsed: false,
            onToggleCollapse: {},
            onVote: { _ in },
            onReply: {},
            onQuote: {},
            onSave: {},
            onReport: nil,
            onEdit: nil,
            onDelete: nil,
            onLock: nil,
            isSaved: false,
            currentUserId: "other"
        )
        XCTAssertNoThrow(try section.inspect().find(button: "Reply"))
        XCTAssertThrowsError(try section.inspect().find(button: "Quote"))

        var surface = NativeCommentThreadSurface(
            client: nil,
            routeMatch: nil,
            currentUserId: "other",
            isSignedIn: true,
            showSignIn: {}
        )
        surface.viewModel.rootPost = post
        XCTAssertEqual(surface.quoteMarkdown(post), "")
        surface.startReply(post: post, quote: true)
        XCTAssertNil(surface.composer)
        XCTAssertThrowsError(try surface.inspect().find(HnDiscussionsPanel.self))

        var staffSurface = NativeCommentThreadSurface(
            client: nil,
            routeMatch: nil,
            currentUserId: "moderator",
            isSignedIn: true,
            isStaffViewer: true,
            showSignIn: {}
        )
        staffSurface.viewModel.rootPost = post
        XCTAssertTrue(staffSurface.quoteMarkdown(post).contains("hidden-story"))
        XCTAssertNoThrow(try staffSurface.inspect().find(HnDiscussionsPanel.self))
    }
}
