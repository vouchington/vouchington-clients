import Foundation
import VouchaAPI
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class RssFeedDeletionPresentationTests: NativeRouteSurfaceViewModelTestCase {
    func testPublishedDeletionOutcomesSelectLocalizedPresentationKeys() async throws {
        let feedId = "70000000-0000-7000-8000-000000003680"
        let path = "/api/v1/rss-feeds/\(feedId)"
        let cases: [(String, (Int, UiMessageKey))] = [
            ("accepted", (202, .sharedRssFeedDeletionAccepted)),
            ("retained-import-conflict", (409, .sharedRssFeedDeletionBlockedByAdminImports))
        ]
        for (fixture, (status, key)) in cases {
            CannedFeedURLProtocol.handlers[path] = (
                ApiFixtureLoader.data("native.rss-feeds.delete.\(fixture)"), status
            )
            let outcome = try await makeClient().deleteRssFeed(rssFeedId: feedId)
            XCTAssertEqual(outcome.messageKey, key)
            for locale in [Locale(identifier: "en"), Locale(identifier: "es")] {
                XCTAssertFalse(UiMessages.string(outcome.messageKey, locale: locale).isEmpty)
            }
        }
    }
}
