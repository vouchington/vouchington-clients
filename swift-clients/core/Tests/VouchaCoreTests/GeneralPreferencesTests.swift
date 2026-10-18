import Foundation
import VouchaModels
import XCTest

final class GeneralPreferencesTests: XCTestCase {
    func testRoundTripRetainsAllTwelveFieldsIncludingFalseAndExplicitNulls() throws {
        let payload = settings()
        let preferences = try decode(payload)
        XCTAssertFalse(preferences.shouldImportHackerNewsDiscussions)
        XCTAssertNil(preferences.country)
        XCTAssertNil(preferences.uiLocale)
        XCTAssertEqual(preferences.followersVisibility, .users)
        XCTAssertEqual(preferences.defaultPostBroadcast, .followers)
        XCTAssertEqual(preferences.defaultPostPrivacy, .private)

        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        let encoded = try XCTUnwrap(JSONSerialization.jsonObject(with: encoder.encode(preferences)) as? NSDictionary)
        XCTAssertEqual(encoded.count, 12)
        XCTAssertTrue(encoded.isEqual(to: payload))
    }

    func testNullableKeysAreRequiredAndNonNullValuesRemainVerbatim() throws {
        for key in ["country", "ui_locale"] {
            var incomplete = settings()
            incomplete.removeValue(forKey: key)
            XCTAssertThrowsError(try decode(incomplete), key)
        }
        var values = settings()
        values["country"] = "US"
        values["ui_locale"] = "fr"
        let preferences = try decode(values)
        XCTAssertEqual(preferences.country, "US")
        XCTAssertEqual(preferences.uiLocale, "fr")
    }

    func testUnknownAudiencePrivacyAndWrongScalarTypesAreRejected() throws {
        let audienceKeys = [
            "follows_visibility", "topic_follows_visibility", "rss_feed_follows_visibility",
            "community_memberships_visibility", "followers_visibility", "likes_visibility",
            "direct_messages_audience", "default_post_broadcast", "default_post_privacy"
        ]
        for key in audienceKeys {
            var values = settings()
            values[key] = "unrecognized"
            XCTAssertThrowsError(try decode(values), key)
        }
        for key in ["country", "ui_locale", "should_import_hacker_news_discussions"] {
            var values = settings()
            values[key] = 42
            XCTAssertThrowsError(try decode(values), key)
        }
    }

    private func decode(_ values: [String: Any]) throws -> GeneralPreferences {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        return try decoder.decode(GeneralPreferences.self, from: JSONSerialization.data(withJSONObject: values))
    }

    private func settings() -> [String: Any] {
        [
            "follows_visibility": "everyone", "topic_follows_visibility": "followers",
            "rss_feed_follows_visibility": "mutual_followers", "community_memberships_visibility": "nobody",
            "followers_visibility": "users", "likes_visibility": "nobody",
            "direct_messages_audience": "mutual_followers", "default_post_broadcast": "followers",
            "default_post_privacy": "private", "country": NSNull(), "ui_locale": NSNull(),
            "should_import_hacker_news_discussions": false
        ]
    }
}
