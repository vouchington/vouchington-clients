import Foundation
import ViewInspector
@testable import VouchaDesignSystem
import VouchaModels
import XCTest

@MainActor
final class PostCardTests: XCTestCase {
    func testHandlerlessPostCardVoteControlsDisableCasting() throws {
        let sut = PostCard(post: makePost())

        XCTAssertFalse(try sut.inspect().find(VoteControls.self).actualView().canCreateVote)
    }

    func testPostCardCallbackSupportsCastingAndRetractingHydratedBallot() throws {
        var choices: [ElectionVoteChoice?] = []
        let sut = PostCard(
            post: makePost(),
            myVote: .like,
            onVote: { choices.append($0) }
        )

        let inspection = try sut.inspect()
        XCTAssertTrue(try inspection.find(VoteControls.self).actualView().canCreateVote)
        try inspection.find(button: "Vouch").tap()
        try inspection.find(button: "Neutral").tap()
        XCTAssertThrowsError(try inspection.find(button: "Clear"))

        XCTAssertEqual(choices, [.vouch, .neutral])
    }

    func testPostCardSignedOutTapInvokesCallback() throws {
        var tapped = false
        let sut = PostCard(
            post: makePost(),
            myVote: nil,
            onSignedOutTap: { tapped = true }
        )

        let buttons = try sut.inspect().findAll(ViewType.Button.self)
        try buttons[0].tap()
        XCTAssertTrue(tapped)
        XCTAssertThrowsError(try sut.inspect().find(text: "2.25"))
    }

    func testPostCardRendersRelationButtonsWhenActionsPresent() throws {
        var saveTapped = false
        var hideTapped = false
        let sut = PostCard(
            post: makePost(),
            myVote: nil,
            onToggleSaved: { saveTapped = true },
            onToggleHidden: { hideTapped = true }
        )

        try sut.inspect().find(button: "Save").tap()
        try sut.inspect().find(button: "Hide").tap()

        XCTAssertTrue(saveTapped)
        XCTAssertTrue(hideTapped)
    }

    func testPostCardRendersNativeMarkdownBodyInCompactAndExpandedModes() throws {
        let post = makePost(title: nil, markdown: "**Body**", html: "<p><strong>Body</strong></p>")
        let compact = PostCard(post: post, variant: .compact)
        let expanded = PostCard(post: post, variant: .expanded)

        XCTAssertEqual(try compact.inspect().find(text: "Body\n\n").string(), "Body\n\n")
        XCTAssertEqual(try expanded.inspect().find(text: "Body\n\n").string(), "Body\n\n")
    }

    func testWhitespaceTitleUsesTrimmedMarkdownAsTheAuthoredCompactFallback() throws {
        let post = makePost(title: " \n\t ", markdown: "  Fallback  ", html: nil)
        let compact = PostCard(post: post, variant: .compact)

        XCTAssertEqual(try compact.inspect().find(text: "Fallback").string(), "Fallback")
        XCTAssertThrowsError(try compact.inspect().find(text: " \n\t "))
    }

    func testUserRowRendersMarkdownBioNatively() throws {
        let user = try makePublicUser(markdown: "_Native bio_")
        let sut = UserRow(user: user, avatarURL: nil)

        XCTAssertEqual(try sut.inspect().find(text: "_Native bio_").string(), "_Native bio_")
    }

    private func makePost() -> Post {
        makePost(title: "Discussion", markdown: "Body", html: nil)
    }

    private func makePost(title: String?, markdown: String?, html: String?) -> Post {
        Post(
            id: "post-1",
            slug: "post-1",
            postType: .discussion,
            title: title,
            markdown: markdown,
            html: html,
            parentId: nil,
            rootId: nil,
            createdById: "user-1",
            createdAt: Date(timeIntervalSince1970: 0),
            broadcast: nil,
            privacy: .public,
            isAnonymous: false,
            communityId: nil,
            clearanceStatus: nil,
            election: PostElection(votesScoreNet: 2.25, votesCountUp: 3, votesCountDown: 1)
        )
    }

    private func makePublicUser(markdown: String?) throws -> PublicUser {
        let markdownValue = markdown.map { "\"\($0)\"" } ?? "null"
        let data = Data(
            """
            {
              "id": "user-1",
              "username": "alice",
              "name": "Alice",
              "roles": ["user"],
              "markdown": \(markdownValue)
            }
            """.utf8
        )
        return try JSONDecoder().decode(PublicUser.self, from: data)
    }
}
