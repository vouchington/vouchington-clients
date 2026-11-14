import Foundation
import SwiftUI
import ViewInspector
import VouchaAPI
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class NativeChatMCPGeneralPreferenceFactTests: XCTestCase {
    func testCanonicalFactKeepsAllElevenSettingsIncludingNull() throws {
        let fixture = try NativeChatMCPGeneralPreferenceFixture()
        let preferences = try preferences(fixture)
        XCTAssertEqual(preferences.followsVisibility, .everyone)
        XCTAssertEqual(preferences.topicFollowsVisibility, .followers)
        XCTAssertEqual(preferences.rssFeedFollowsVisibility, .mutualFollowers)
        XCTAssertEqual(preferences.communityMembershipsVisibility, .nobody)
        XCTAssertEqual(preferences.followersVisibility, .users)
        XCTAssertEqual(preferences.likesVisibility, .nobody)
        XCTAssertEqual(preferences.directMessagesAudience, .mutualFollowers)
        XCTAssertEqual(preferences.defaultPostBroadcast, .followers)
        XCTAssertEqual(preferences.defaultPostPrivacy, .private)
        XCTAssertEqual(preferences.country, "US")
        XCTAssertNil(preferences.uiLocale)

        let settings = try XCTUnwrap(fixture.structured["settings"] as? [String: Any])
        for key in settings.keys {
            var missing = settings
            missing.removeValue(forKey: key)
            var incomplete = fixture
            incomplete.structured["settings"] = missing
            XCTAssertNil(try NativeChatMCPFact.makeGeneralPreferences(incomplete.value), key)
        }
        var failed = fixture
        failed.structured["success"] = false
        XCTAssertNil(try NativeChatMCPFact.makeGeneralPreferences(failed.value))
    }

    func testAllRowsUseLocalizedLabelsAndExactAudienceMeaningWithoutRawEnvelopeOrControls() throws {
        let fixture = try NativeChatMCPGeneralPreferenceFixture()
        let preferences = try preferences(fixture)
        let labels: [UiMessageKey] = [
            .nativeSwiftSettingsFollows, .nativeSwiftSettingsTopicFollows, .nativeSwiftSettingsRssFollows,
            .nativeSwiftSettingsCommunityMemberships, .nativeSwiftSettingsFollowers, .nativeSwiftSettingsLikes,
            .nativeSwiftSettingsDirectMessages, .nativeSwiftSettingsDefaultPostAudience,
            .nativeSwiftSettingsDefaultPostPrivacy, .settingsLanguageCountryLabel, .settingsLanguageInterfaceLabel
        ]
        for locale in [Locale(identifier: "en"), Locale(identifier: "fr")] {
            let inspected = try NativeChatGeneralPreferencesFactView(preferences: preferences, locale: locale).inspect()
            XCTAssertEqual(inspected.findAll(ViewType.HStack.self).count, 11)
            for label in labels {
                XCTAssertNoThrow(try inspected.find(text: UiMessages.string(label, locale: locale)))
            }
            let values: [UiMessageKey] = [
                .nativeSwiftSettingsEveryone, .extractedMyAudienceSelectLoggedInUsers81dac12b,
                .nativeSwiftSettingsFollowers, .nativeSwiftSettingsMutualFollowers,
                .nativeSwiftPresentationValuesNobody, .nativeSwiftSettingsPrivate,
                .settingsLanguageUseSiteDefault
            ]
            for value in values {
                XCTAssertNoThrow(try inspected.find(text: UiMessages.string(value, locale: locale)))
            }
            XCTAssertNoThrow(try inspected.find(text: "US"))
            XCTAssertThrowsError(try inspected.find(ViewType.Toggle.self))
            XCTAssertThrowsError(try inspected.find(ViewType.Picker.self))
            let raw = try String(data: fixture.value.exactJSONData(), encoding: .utf8)
            XCTAssertThrowsError(try inspected.find(text: XCTUnwrap(raw)))
        }
    }

    func testNullCountryAndExplicitLocaleRemainDistinctFromSiteDefault() throws {
        var fixture = try NativeChatMCPGeneralPreferenceFixture()
        var settings = try XCTUnwrap(fixture.structured["settings"] as? [String: Any])
        settings["country"] = NSNull()
        settings["ui_locale"] = "fr"
        settings["default_post_privacy"] = "public"
        fixture.structured["settings"] = settings
        let preferences = try preferences(fixture)
        for locale in [Locale(identifier: "en"), Locale(identifier: "fr")] {
            let inspected = try NativeChatGeneralPreferencesFactView(preferences: preferences, locale: locale).inspect()
            XCTAssertNoThrow(try inspected.find(text: UiMessages.string(
                .settingsLanguageNoCountryPreference,
                locale: locale
            )))
            XCTAssertNoThrow(try inspected.find(text: "fr"))
            XCTAssertNoThrow(try inspected.find(text: UiMessages.string(.nativeSwiftSettingsPublic, locale: locale)))
            XCTAssertThrowsError(try inspected.find(text: UiMessages.string(
                .settingsLanguageUseSiteDefault,
                locale: locale
            )))
        }
    }

    private func preferences(_ fixture: NativeChatMCPGeneralPreferenceFixture) throws -> GeneralPreferences {
        guard case let .generalPreferences(preferences)? = try NativeChatMCPFact.makeGeneralPreferences(fixture.value)
        else {
            throw XCTUnwrapError.expectedFact
        }
        return preferences
    }

    private enum XCTUnwrapError: Error { case expectedFact }
}
