import Foundation
import SwiftUI
import ViewInspector
import VouchaAPI
import VouchaCore
import VouchaDesignSystem
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class PostAuthorAvatarRenderingTests: XCTestCase {
    private let expectedURL =
        "https://images.voucha.ai/images/placements/00000000-0000-7000-8000-000000000301/2/00000000-0000-7000-8000-000000000201?w=48"

    func testRootAncestorAndCommentRenderTheClaimedAuthorPlacement() throws {
        let decoder = fixtureDecoder()
        let root = try decoder.decode(
            PostEnvelope.self,
            from: ApiFixtureLoader.data("native.comments.post-detail.default")
        ).post
        let descendants = try decoder.decode(
            PostThreadEnvelope.self,
            from: ApiFixtureLoader.data("native.comments.descendants.default")
        )
        let ancestors = try decoder.decode(
            PostThreadEnvelope.self,
            from: ApiFixtureLoader.data("native.comments.ancestors.permalink")
        )
        let rootView = section(for: root, isRoot: true, title: root.title ?? "")
            .environment(\.locale, Locale(identifier: "en"))
        let ancestor = try XCTUnwrap(ancestors.posts["comment-a"])
        let ancestorView = section(for: ancestor, isRoot: false, title: ancestor.createdBy?.username ?? "")
            .environment(\.locale, Locale(identifier: "en"))
        let comment = try XCTUnwrap(descendants.posts["comment-a"])
        let commentView = nodeView(for: comment).environment(\.locale, Locale(identifier: "en"))

        try assertPlacement(in: rootView)
        try assertPlacement(in: ancestorView)
        try assertPlacement(in: commentView)
    }

    func testAnonymousAndDeletedCommentAuthorsNeverRenderPlacementImages() throws {
        let response = try fixtureDecoder().decode(
            PostThreadEnvelope.self,
            from: ApiFixtureLoader.data("native.comments.descendants.default")
        )
        let anonymous = try XCTUnwrap(response.posts["comment-c"])
        let deleted = try XCTUnwrap(response.posts["comment-d"])

        for post in [anonymous, deleted] {
            let rendered = try nodeView(for: post)
                .environment(\.locale, Locale(identifier: "en"))
                .inspect()
            XCTAssertTrue(rendered.findAll(ViewType.View<AsyncImageView>.self).isEmpty)
        }
    }

    func testThreadUsesActiveClientImageHostAndHidesDecorativeAvatarFromAccessibility() throws {
        let decoder = fixtureDecoder()
        let root = try decoder.decode(
            PostEnvelope.self,
            from: ApiFixtureLoader.data("native.comments.post-detail.default")
        ).post
        let descendants = try decoder.decode(
            PostThreadEnvelope.self,
            from: ApiFixtureLoader.data("native.comments.descendants.default")
        )
        let ancestors = try decoder.decode(
            PostThreadEnvelope.self,
            from: ApiFixtureLoader.data("native.comments.ancestors.permalink")
        )
        let imageHost = try XCTUnwrap(URL(string: "https://images.self-hosted.example"))
        let config = try AppConfig(
            baseURL: XCTUnwrap(URL(string: "https://api.self-hosted.example")),
            imageBaseURL: imageHost
        )
        let client = APIClient(config: config, protocolClasses: [])
        let surface = NativeCommentThreadSurface(
            client: client,
            routeMatch: nil,
            isSignedIn: false,
            showSignIn: {}
        )
        surface.viewModel.rootPost = root
        surface.viewModel.ancestorPosts = try [XCTUnwrap(ancestors.posts["comment-a"])]
        surface.viewModel.commentTree = try [
            NativeCommentThreadNode(post: XCTUnwrap(descendants.posts["comment-a"]))
        ]

        let images = try surface.inspect().findAll(ViewType.View<AsyncImageView>.self)
        XCTAssertEqual(images.count, 3)
        for image in images {
            XCTAssertTrue(try image.accessibilityHidden())
            XCTAssertTrue(try XCTUnwrap(image.actualView().resolvedURLString).hasPrefix(imageHost.absoluteString))
        }
    }

    private func fixtureDecoder() -> JSONDecoder {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return decoder
    }

    private func assertPlacement(in view: some View, file: StaticString = #filePath, line: UInt = #line) throws {
        let image = try view.inspect().find(ViewType.View<AsyncImageView>.self)
        XCTAssertEqual(try image.actualView().resolvedURLString, expectedURL, file: file, line: line)
    }

    private func section(
        for post: Post,
        isRoot: Bool,
        title: String,
        imageConfig: AppConfig = .shared
    ) -> CommentThreadPostSection {
        CommentThreadPostSection(
            title: .verbatim(title),
            post: post,
            imageConfig: imageConfig,
            embed: nil,
            pathText: "/discussion/root/comment/\(post.id)",
            voteChoice: nil,
            isSignedIn: false,
            canCreateVote: false,
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

    private func nodeView(for post: Post) -> NativeCommentThreadNodeView {
        NativeCommentThreadNodeView(
            node: NativeCommentThreadNode(post: post),
            rootPostId: "comment-root-post",
            rootPostType: "discussion",
            depth: 0,
            collapsedIds: [],
            isSignedIn: false,
            canCreateVote: false,
            showSignIn: {},
            currentUserId: nil,
            hideDownCount: false,
            inFlightVotePostIds: [],
            bookmarksByPostId: [:],
            postEmbedsByPostId: [:],
            voteChoiceByPostId: [:],
            onToggleCollapse: { _ in },
            onVote: { _, _ in },
            onReply: { _ in },
            onQuote: { _ in },
            onSave: { _ in },
            onReport: { _ in },
            onEdit: { _ in },
            onDelete: { _ in },
            onLock: { _ in }
        )
    }
}
