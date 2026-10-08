import Foundation
import SwiftUI
import ViewInspector
import VouchaDesignSystem
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class AccountTypeAuthorSurfaceTests: NativeRouteSurfaceViewModelTestCase {
    private let cases: [(String?, String?)] = [
        ("official", "Official"), ("system", "System"), ("ai_agent", "AI Agent"), (nil, nil)
    ]

    func testPostCardsAndUserRowsRenderEveryAccountClassification() throws {
        for (accountType, label) in cases {
            let author = try user(accountType)
            for variant in [CardVariant.compact, .expanded] {
                try assertLabel(PostCard(post: post(author), variant: variant), expected: label)
            }
            try assertLabel(UserRow(user: author, avatarURL: nil), expected: label)
        }
    }

    func testRootAndCommentHeadersRenderEveryAccountClassification() throws {
        for (accountType, label) in cases {
            for isRoot in [true, false] {
                let view = try CommentThreadPostSection(
                    title: .verbatim("Author"), post: post(user(accountType)), embed: nil,
                    pathText: "/discussion/post", voteChoice: nil, isSignedIn: true,
                    canCreateVote: true, showSignIn: {}, hideDownCount: false,
                    isRoot: isRoot, isCollapsed: false, onToggleCollapse: {}, onVote: { _ in },
                    onReply: {}, onQuote: {}, onSave: {}, onReport: nil, onEdit: nil,
                    onDelete: nil, onLock: nil, isSaved: false
                )
                try assertLabel(view, expected: label)
            }
        }
    }

    func testLoadedProfileHeaderRendersEveryAccountClassification() async throws {
        for (accountType, label) in cases {
            var response = try XCTUnwrap(
                JSONSerialization.jsonObject(with: ApiFixtureLoader.data("native.users.profile.default"))
                    as? [String: Any]
            )
            var author = try XCTUnwrap(response["user"] as? [String: Any])
            author["account_type"] = accountType as Any? ?? NSNull()
            response["user"] = author
            CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = try (
                JSONSerialization.data(withJSONObject: response), 200
            )
            let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice"))
            let viewModel = try NativeRouteSurfaceViewModel(
                entry: route.entry, client: makeClient(), routeMatch: route.match
            )
            await viewModel.load()
            let view = NativeUserProfileSurface(
                entry: route.entry, viewModel: viewModel, isSignedIn: true,
                turnstileSiteKey: nil, showSignIn: {}, onNavigate: { _ in }
            )
            try assertLabel(view, expected: label)
        }
    }

    private func assertLabel(_ view: some View, expected: String?) throws {
        let badge = try view.inspect().find(AccountTypeBadge.self)
        if let expected {
            XCTAssertEqual(try badge.find(text: expected).string(), expected)
        } else {
            XCTAssertTrue(try badge.findAll(ViewType.Text.self).isEmpty)
        }
    }

    private func user(_ accountType: String?) throws -> PublicUser {
        let data = try JSONSerialization.data(withJSONObject: [
            "id": "author", "username": "alice", "account_type": accountType as Any? ?? NSNull()
        ])
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        return try decoder.decode(PublicUser.self, from: data)
    }

    private func post(_ author: PublicUser) -> Post {
        Post(
            id: "post", slug: "post", postType: .discussion, title: "Discussion", markdown: nil,
            html: nil, parentId: nil, rootId: nil, createdById: author.id,
            createdAt: Date(timeIntervalSince1970: 0), broadcast: nil, privacy: .public,
            isAnonymous: false, communityId: nil, clearanceStatus: nil,
            election: nil, createdBy: author
        )
    }
}
