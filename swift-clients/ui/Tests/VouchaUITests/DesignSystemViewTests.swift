import ViewInspector
@testable import VouchaDesignSystem
import VouchaModels
import XCTest

@MainActor
final class DesignSystemViewTests: XCTestCase {
    private let apiBaseURL = URL(string: "http://localhost:2999")!

    func testEmptyStateViewRendersTitle() throws {
        let sut = EmptyStateView(icon: "tray", title: .verbatim("No items"))
        let title = try sut.inspect().find(text: "No items")
        XCTAssertEqual(try title.string(), "No items")
    }

    func testEmptyStateViewRendersMessageAndActionButton() throws {
        var tapped = false
        let sut = EmptyStateView(
            icon: "tray",
            title: .verbatim("No items"),
            message: .verbatim("Nothing here yet"),
            actionTitle: .verbatim("Reload"),
            action: { tapped = true }
        )
        XCTAssertEqual(
            try sut.inspect().find(text: "Nothing here yet").string(),
            "Nothing here yet"
        )
        try sut.inspect().find(button: "Reload").tap()
        XCTAssertTrue(tapped)
    }

    func testRssFeedItemCardRendersCompactVoteControlsAndTapsVotes() throws {
        var choices: [ElectionVoteChoice?] = []
        let sut = RssFeedItemCard(
            item: makeRssFeedItem(),
            myVote: nil,
            onVote: { choice in
                choices.append(choice)
            },
            apiBaseURL: apiBaseURL
        )

        XCTAssertEqual(try sut.inspect().find(text: "Item title").string(), "Item title")
        XCTAssertEqual(try sut.inspect().find(text: "Author").string(), "Author")
        XCTAssertEqual(try sut.inspect().find(text: "2").string(), "2")

        XCTAssertTrue(choices.isEmpty)
    }

    func testRssFeedItemCardRendersExpandedRawVoteCounts() throws {
        var choices: [ElectionVoteChoice?] = []
        let sut = RssFeedItemCard(
            item: makeRssFeedItem(),
            variant: .expanded,
            myVote: .like,
            onVote: { choice in
                choices.append(choice)
            },
            apiBaseURL: apiBaseURL
        )

        XCTAssertEqual(try sut.inspect().find(text: "Item title").string(), "Item title")
        XCTAssertEqual(try sut.inspect().find(text: "Item description").string(), "Item description")
        XCTAssertEqual(try sut.inspect().find(text: "2").string(), "2")

        XCTAssertTrue(choices.isEmpty)
    }

    func testRssFeedItemCardOmitsVoteControlsWithoutElection() throws {
        let sut = RssFeedItemCard(item: makeRssFeedItem(election: nil), apiBaseURL: apiBaseURL)

        XCTAssertEqual(try sut.inspect().find(text: "Item title").string(), "Item title")
        XCTAssertTrue(try sut.inspect().findAll(ViewType.Button.self).isEmpty)
    }

    func testPostCardRendersRawPositiveAndNegativeVoteCounts() throws {
        let sut = PostCard(post: makePost(), myVote: .like)

        XCTAssertEqual(try sut.inspect().find(text: "Post title").string(), "Post title")
        XCTAssertEqual(try sut.inspect().find(text: "4").string(), "4")
        XCTAssertEqual(try sut.inspect().find(text: "1").string(), "1")
    }

    func testVoteControlsHidesRestrictedNegativeCount() throws {
        let sut = VoteControls(
            election: PostElection(votesScoreNet: 3, votesCountUp: 4, votesCountDown: 1),
            hideDownCount: true
        )

        XCTAssertEqual(try sut.inspect().find(text: "4").string(), "4")
        XCTAssertThrowsError(try sut.inspect().find(text: "1"))
    }

    func testPostCardThreadsRestrictedNegativeCount() throws {
        let sut = PostCard(post: makePost(), myVote: .like, hideDownCount: true)

        XCTAssertNoThrow(try sut.inspect().find(text: "4"))
        XCTAssertThrowsError(try sut.inspect().find(text: "1"))
    }

    func testPostCardDoesNotRenderWeightedVoteScore() throws {
        let sut = PostCard(
            post: makePost(election: .init(votesScoreNet: 3.5, votesCountUp: 4, votesCountDown: 1, myVote: nil)),
            myVote: .like
        )

        XCTAssertThrowsError(try sut.inspect().find(text: "3.5"))
    }

    func testTopicRecommendationPostCardUsesRecommendationBallot() throws {
        let sut = PostCard(
            post: makePost(postType: .topicRecommendation),
            myVote: .support,
            onVote: { _ in }
        )

        let voteControls = try sut.inspect().find(VoteControls.self)
        XCTAssertEqual(try voteControls.actualView().policy.choices, ElectionVotePolicy.recommendation.choices)
        XCTAssertNoThrow(try voteControls.find(button: "Support"))
        XCTAssertNoThrow(try voteControls.find(button: "Oppose"))
        XCTAssertThrowsError(try voteControls.find(button: "Clear"))
        XCTAssertThrowsError(try voteControls.find(button: "Vouch"))
        XCTAssertThrowsError(try voteControls.find(button: "Neutral"))
    }

    func testOfficialViewerCanClearAnExistingBallotButCannotCreateOrReplaceIt() throws {
        var choices: [ElectionVoteChoice?] = []
        let sut = VoteControls(
            election: PostElection(votesScoreNet: 2, votesCountUp: 3, votesCountDown: 1),
            myVote: .like,
            canCreateVote: false,
            onVote: { choices.append($0) }
        )

        XCTAssertNoThrow(try sut.inspect().find(button: "Clear").tap())
        XCTAssertEqual(choices, [nil])
        XCTAssertThrowsError(try sut.inspect().find(button: "Vouch"))
        XCTAssertThrowsError(try sut.inspect().find(button: "Dislike"))
    }

    func testOfficialViewerWithoutABallotCannotOpenAVoteMenu() throws {
        let sut = VoteControls(
            election: PostElection(votesScoreNet: 0, votesCountUp: 3, votesCountDown: 1),
            canCreateVote: false,
            onVote: { _ in }
        )

        XCTAssertTrue(try sut.inspect().find(ViewType.Menu.self).isDisabled())
        XCTAssertThrowsError(try sut.inspect().find(button: "Vouch"))
    }

    func testSignedOutViewerWithoutABallotCanOpenSignInFromVoteMenu() throws {
        var signInCount = 0
        let sut = VoteControls(
            election: PostElection(votesScoreNet: 0, votesCountUp: 3, votesCountDown: 1),
            canCreateVote: false,
            onSignedOutTap: { signInCount += 1 }
        )

        XCTAssertFalse(try sut.inspect().find(ViewType.Menu.self).isDisabled())
        try sut.inspect().find(button: "Sign In").tap()

        XCTAssertEqual(signInCount, 1)
        XCTAssertThrowsError(try sut.inspect().find(button: "Vouch"))
        XCTAssertThrowsError(try sut.inspect().find(button: "Clear"))
    }

    func testSignedOutViewerWithHistoricalBallotCanOpenSignInButCannotClear() throws {
        var signInCount = 0
        let sut = VoteControls(
            election: PostElection(votesScoreNet: 0, votesCountUp: 3, votesCountDown: 1),
            myVote: .like,
            canCreateVote: false,
            onSignedOutTap: { signInCount += 1 }
        )

        try sut.inspect().find(button: "Sign In").tap()

        XCTAssertEqual(signInCount, 1)
        XCTAssertThrowsError(try sut.inspect().find(button: "Clear"))
    }

    func testVoteControlsShowsVotePromptWhenViewerHasNoBallot() throws {
        let sut = VoteControls(
            election: PostElection(votesScoreNet: 0, votesCountUp: 3, votesCountDown: 1),
            myVote: nil,
            onVote: { _ in }
        )

        XCTAssertNoThrow(try sut.inspect().find(text: "Vote"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Vouch"))
        XCTAssertThrowsError(try sut.inspect().find(button: "Neutral"))
        XCTAssertThrowsError(try sut.inspect().find(button: "Clear"))
    }

    private func makeRssFeedItem(election: RssFeedItemElection? = .init(
        votesScoreNet: 2,
        votesCountUp: 4,
        votesCountDown: 2,
        myVote: nil
    )) -> RssFeedItem {
        RssFeedItem(
            id: "item-1",
            rssFeedId: "feed-1",
            title: "Item title",
            description: "Item description",
            content: nil,
            link: nil,
            publishedAt: Date(timeIntervalSince1970: 0),
            creator: "Author",
            categories: [],
            mediaContent: nil,
            election: election
        )
    }

    private func makePost(
        postType: PostType = .discussion,
        election: PostElection? = .init(
            votesScoreNet: 3,
            votesCountUp: 4,
            votesCountDown: 1,
            myVote: nil
        )
    ) -> Post {
        Post(
            id: "post-1",
            slug: nil,
            postType: postType,
            title: "Post title",
            markdown: "Post body",
            html: nil,
            parentId: nil,
            rootId: nil,
            createdById: "user-1",
            createdAt: Date(timeIntervalSince1970: 0),
            broadcast: nil,
            privacy: .public,
            isAnonymous: false,
            communityId: nil,
            clearanceStatus: nil,
            election: election
        )
    }
}
