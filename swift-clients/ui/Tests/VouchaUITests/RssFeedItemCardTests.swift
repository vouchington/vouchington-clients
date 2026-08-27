import Foundation
import ViewInspector
@testable import VouchaDesignSystem
import VouchaModels
import XCTest

@MainActor
final class RssFeedItemCardTests: XCTestCase {
    private let apiBaseURL = URL(string: "http://localhost:2999")!

    private func makeItem(
        thumbnailURL: String? = nil,
        mediaContentURL: String? = nil
    ) throws -> RssFeedItem {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        let mediaContentJSON = mediaContentURL.map {
            """
            {
              "url":"\($0)",
              "type":"audio/mpeg",
              "medium":"audio",
              "duration":600
            }
            """
        } ?? "null"
        let thumbnailURLJSON = thumbnailURL.map { "\"\($0)\"" } ?? "null"
        let json = Data("""
        {
          "id":"item-1",
          "rss_feed_id":"feed-1",
          "title":"Item item-1",
          "description":"Short summary",
          "content":null,
          "link":null,
          "published_at":"2026-01-01T00:00:00Z",
          "creator":"Author",
          "categories":[],
          "media_content":\(mediaContentJSON),
          "thumbnail_url":\(thumbnailURLJSON)
        }
        """.utf8)
        return try decoder.decode(RssFeedItem.self, from: json)
    }

    func testRssFeedItemCardShowsVoteControlsWhenElectionExists() throws {
        let item = try makeItem()
        let sut = RssFeedItemCard(
            item: item,
            election: RssFeedItemElection(votesScoreNet: 5.5, votesCountUp: 6, votesCountDown: 1),
            onVote: { _ in },
            apiBaseURL: apiBaseURL
        )

        XCTAssertEqual(try sut.inspect().find(text: "6").string(), "6")
        XCTAssertEqual(try sut.inspect().find(text: "1").string(), "1")
        XCTAssertEqual(try sut.inspect().findAll(ViewType.Button.self).count, 4)
        XCTAssertNoThrow(try sut.inspect().find(button: "Vouch"))
        XCTAssertThrowsError(try sut.inspect().find(button: "Neutral"))
        XCTAssertThrowsError(try sut.inspect().find(button: "Clear"))
    }

    func testHandlerlessRssVoteControlsDisableCasting() throws {
        let item = try makeItem()
        let sut = RssFeedItemCard(
            item: item,
            election: RssFeedItemElection(votesScoreNet: 5.5, votesCountUp: 6, votesCountDown: 1),
            apiBaseURL: apiBaseURL
        )

        XCTAssertFalse(try sut.inspect().find(VoteControls.self).actualView().canCreateVote)
    }

    func testRssFeedItemCardSignedOutTapInvokesCallback() throws {
        let item = try makeItem()
        var tapped = false
        let sut = RssFeedItemCard(
            item: item,
            election: RssFeedItemElection(votesScoreNet: 5.5, votesCountUp: 6, votesCountDown: 1),
            onSignedOutTap: { tapped = true },
            apiBaseURL: apiBaseURL
        )

        let buttons = try sut.inspect().findAll(ViewType.Button.self)
        try buttons[0].tap()
        XCTAssertTrue(tapped)
    }

    func testRssFeedItemCardRendersRelationButtonsWhenActionsPresent() throws {
        let item = try makeItem()
        var saveTapped = false
        var hideTapped = false
        let sut = RssFeedItemCard(
            item: item,
            onToggleSaved: { saveTapped = true },
            onToggleHidden: { hideTapped = true },
            apiBaseURL: apiBaseURL
        )

        try sut.inspect().find(button: "Save").tap()
        try sut.inspect().find(button: "Hide").tap()

        XCTAssertTrue(saveTapped)
        XCTAssertTrue(hideTapped)
    }

    func testRssFeedItemCardUsesThumbnailSidecarArtwork() throws {
        let item = try makeItem(
            thumbnailURL: "/sideload/thumb.jpg",
            mediaContentURL: "https://example.com/stream.mp3"
        )
        let sut = RssFeedItemCard(item: item, apiBaseURL: apiBaseURL)

        let artwork = try sut.inspect().find(ViewType.View<AsyncImageView>.self)
        let actual = try artwork.actualView()
        XCTAssertEqual(actual.resolvedURLString, "http://localhost:2999/sideload/thumb.jpg")
    }

    func testExpandedRssFeedItemCardUsesThumbnailSidecarArtwork() throws {
        let item = try makeItem(
            thumbnailURL: "/sideload/expanded-thumb.jpg",
            mediaContentURL: "https://example.com/stream.mp3"
        )
        let sut = RssFeedItemCard(item: item, variant: .expanded, apiBaseURL: apiBaseURL)

        let artwork = try sut.inspect().find(ViewType.View<AsyncImageView>.self)
        XCTAssertEqual(
            try artwork.actualView().resolvedURLString,
            "http://localhost:2999/sideload/expanded-thumb.jpg"
        )
    }

    func testExpandedCardShowsDescriptionByDefaultAndCanSuppressIt() throws {
        let item = try makeItem()

        XCTAssertNoThrow(try RssFeedItemCard(item: item, variant: .expanded).inspect().find(text: "Short summary"))
        XCTAssertThrowsError(
            try RssFeedItemCard(item: item, variant: .expanded, showsDescription: false)
                .inspect().find(text: "Short summary")
        )
    }
}
