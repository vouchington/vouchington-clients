import Foundation
import SwiftUI
import ViewInspector
import VouchaDesignSystem
import VouchaModels
import XCTest

@MainActor
final class HackerNewsReferencesViewTests: XCTestCase {
    private func references(stories: Int = 1, comments: Int = 2, details: Bool = true) throws -> HackerNewsReferences {
        let json: [String: Any] = [
            "storyCount": stories, "commentCount": comments,
            "stories": details ? [[
                "hackerNewsItemId": 101,
                "points": 1_234,
                "replyCount": 8,
                "postedAt": "2026-01-01T00:00:00Z"
            ]] : [],
            "comments": details ? [303, 302].map { [
                "hackerNewsItemId": $0,
                "parentStoryItemId": 101,
                "postedAt": "2026-01-02T00:00:00Z"
            ] as [String: Any] } : []
        ]
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        return try decoder.decode(HackerNewsReferences.self, from: JSONSerialization.data(withJSONObject: json))
    }

    func testCompactSummaryHasNoLinksAndUsesCombinedMentionLabel() throws {
        let view = try HackerNewsReferencesView(references: references())
        XCTAssertEqual(try view.inspect().find(text: "Seen on Hacker News · 1").string(), "Seen on Hacker News · 1")
        XCTAssertEqual(try view.inspect().find(text: "Mentioned · 2").string(), "Mentioned · 2")
        XCTAssertTrue(try view.inspect().findAll(ViewType.Link.self).isEmpty)
    }

    func testCommentOnlyAndAbsentReferences() throws {
        let view = try HackerNewsReferencesView(references: references(stories: 0))
        XCTAssertEqual(
            try view.inspect().find(text: "Mentioned on Hacker News · 2").string(),
            "Mentioned on Hacker News · 2"
        )
        XCTAssertTrue(try HackerNewsReferencesView(references: nil).inspect().findAll(ViewType.Text.self).isEmpty)
        let empty = try HackerNewsReferencesView(
            references: references(stories: 0, comments: 0, details: false),
            expanded: true
        )
        XCTAssertTrue(try empty.inspect().findAll(ViewType.Text.self).isEmpty)
    }

    func testExpandedLinksPreserveServerOrderAndShowStoryStatistics() throws {
        let view = try HackerNewsReferencesView(references: references(), expanded: true)
        let links = try view.inspect().findAll(ViewType.Link.self)
        XCTAssertEqual(try links.map { try $0.url().absoluteString }, [
            "https://news.ycombinator.com/item?id=101",
            "https://news.ycombinator.com/item?id=303",
            "https://news.ycombinator.com/item?id=302"
        ])
        XCTAssertEqual(try view.inspect().find(text: "1,234 points · 8 comments").string(), "1,234 points · 8 comments")
        XCTAssertEqual(try view.inspect().find(text: "Seen on Hacker News").string(), "Seen on Hacker News")
        XCTAssertEqual(try view.inspect().find(text: "Mentioned on Hacker News").string(), "Mentioned on Hacker News")
        XCTAssertEqual(try links[1].labelView().text().string().prefix(11), "Comment on ")
    }

    func testSummaryWithoutDetailArraysDoesNotInventLinks() throws {
        let decoder = JSONDecoder()
        let references = try decoder.decode(
            HackerNewsReferences.self,
            from: Data(#"{"storyCount":1,"commentCount":1}"#.utf8)
        )
        let view = HackerNewsReferencesView(references: references, expanded: true)
        XCTAssertTrue(try view.inspect().findAll(ViewType.Link.self).isEmpty)
    }
}
