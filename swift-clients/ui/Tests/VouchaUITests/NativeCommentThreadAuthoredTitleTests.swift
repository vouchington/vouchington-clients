import Foundation
import SwiftUI
import ViewInspector
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class NativeCommentThreadAuthoredTitleTests: XCTestCase {
    func testOnlyRootPostTitleUsesAuthoredLanguageDirection() throws {
        let post = makePost()
        let root = makeSection(title: "عنوان", post: post, isRoot: true)
        let comment = makeSection(title: "كاتب", post: post, isRoot: false)

        XCTAssertEqual(
            try root.inspect().find(text: "عنوان").environment(\.layoutDirection),
            .rightToLeft
        )
        XCTAssertThrowsError(
            try comment.inspect().find(text: "كاتب").environment(\.layoutDirection)
        )
    }

    private func makeSection(title: String, post: Post, isRoot: Bool) -> CommentThreadPostSection {
        CommentThreadPostSection(
            title: .verbatim(title),
            post: post,
            embed: nil,
            pathText: "/discussion/root-1",
            voteChoice: nil,
            isSignedIn: true,
            canCreateVote: true,
            showSignIn: {},
            hideDownCount: false,
            isRoot: isRoot,
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
            isSaved: false
        )
    }

    private func makePost() -> Post {
        Post(
            id: "root-1",
            slug: nil,
            postType: .discussion,
            title: "عنوان",
            declaredLanguage: "ar",
            markdown: nil,
            html: nil,
            parentId: nil,
            rootId: nil,
            createdById: "user-1",
            createdAt: Date(timeIntervalSince1970: 10),
            broadcast: .everyone,
            privacy: .public,
            isAnonymous: false,
            communityId: nil,
            clearanceStatus: nil
        )
    }
}
