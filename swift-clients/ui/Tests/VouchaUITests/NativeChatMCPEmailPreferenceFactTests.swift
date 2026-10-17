import Foundation
import SwiftUI
import ViewInspector
import VouchaAPI
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class NativeChatMCPEmailPreferenceFactTests: XCTestCase {
    func testValidatedEmailFactDecodesEightFieldsAndRejectsUnknownOrIncompleteValues() throws {
        let value = try decodedValue(successfulPayload)
        guard case let .emailPreferences(preferences)? = NativeChatMCPFact.makeEmailPreferences(value) else {
            return XCTFail("Expected the complete email-preferences fact")
        }

        XCTAssertTrue(preferences.engagementEmailsEnabled)
        XCTAssertEqual(preferences.newsDigestFrequency, "weekly")
        XCTAssertFalse(preferences.moderationEmailsEnabled)
        XCTAssertEqual(preferences.communityDigestFrequency, "none")
        XCTAssertEqual(preferences.moderationEmailCadence, "selected_days")
        XCTAssertEqual(preferences.moderationEmailDaysOfWeek, [1, 3, 5])
        XCTAssertEqual(preferences.moderationEmailTimeOfDay, "09:30")
        XCTAssertEqual(preferences.moderationEmailTimezone, "Europe/Paris")

        let unknown = try decodedValue(successfulPayload.replacingOccurrences(of: "weekly", with: "fortnightly"))
        XCTAssertNil(NativeChatMCPFact.makeEmailPreferences(unknown))
        let missing = try decodedValue(#"{"success":true,"email_preferences":{"is_engagement_emails_enabled":true}}"#)
        XCTAssertNil(NativeChatMCPFact.makeEmailPreferences(missing))
        let failed = try decodedValue(successfulPayload.replacingOccurrences(
            of: "\"success\":true",
            with: "\"success\":false"
        ))
        XCTAssertNil(NativeChatMCPFact.makeEmailPreferences(failed))
    }

    func testEmailFactUsesLocalizedReadOnlyRowsWithoutShowingEnvelope() throws {
        let value = try decodedValue(successfulPayload)
        guard case let .emailPreferences(preferences)? = NativeChatMCPFact.makeEmailPreferences(value) else {
            return XCTFail("Expected email preferences")
        }

        for locale in [Locale(identifier: "en"), Locale(identifier: "fr")] {
            let inspected = try NativeChatEmailPreferencesFactView(preferences: preferences, locale: locale).inspect()
            XCTAssertNoThrow(try inspected.find(text: UiMessages.string(
                .nativeSwiftSettingsEngagementEmails,
                locale: locale
            )))
            XCTAssertNoThrow(try inspected.find(text: UiMessages.string(
                .nativeSwiftSettingsNewsDigest,
                locale: locale
            )))
            XCTAssertNoThrow(try inspected.find(text: UiMessages.string(
                .nativeSwiftSettingsModerationEmails,
                locale: locale
            )))
            XCTAssertNoThrow(try inspected.find(text: UiMessages.string(
                .nativeSwiftSettingsCommunityDigest,
                locale: locale
            )))
            XCTAssertNoThrow(try inspected.find(text: UiMessages.string(
                .nativeSwiftSettingsModerationCadence,
                locale: locale
            )))
            XCTAssertNoThrow(try inspected.find(text: UiMessages.string(
                .nativeSwiftSettingsModerationDays,
                locale: locale
            )))
            XCTAssertNoThrow(try inspected.find(text: UiMessages.string(.nativeSwiftRouteSurfaceTrue, locale: locale)))
            XCTAssertNoThrow(try inspected.find(text: UiMessages.string(.nativeSwiftRouteSurfaceFalse, locale: locale)))
            XCTAssertNoThrow(try inspected.find(text: UiMessages.string(.nativeSwiftSettingsWeekly, locale: locale)))
            XCTAssertNoThrow(try inspected.find(text: UiMessages.string(.nativeSwiftSettingsOff, locale: locale)))
            XCTAssertNoThrow(try inspected.find(text: UiMessages.string(
                .nativeSwiftSettingsSelectedDays,
                locale: locale
            )))
            XCTAssertNoThrow(try inspected.find(text: "09:30"))
            XCTAssertNoThrow(try inspected.find(text: "Europe/Paris"))
            XCTAssertThrowsError(try inspected.find(ViewType.Toggle.self))
            XCTAssertThrowsError(try inspected.find(text: successfulPayload))
        }
    }

    private func decodedValue(_ json: String) throws -> DecodedJSONValue {
        try DecodedJSONValue(exactJSONData: Data(json.utf8))
    }

    private let successfulPayload = #"{"success":true,"email_preferences":{"is_engagement_emails_enabled":true,"news_digest_frequency":"weekly","is_moderation_emails_enabled":false,"community_digest_frequency":"none","moderation_email_cadence":"selected_days","moderation_email_days_of_week":[1,3,5],"moderation_email_time_of_day":"09:30","moderation_email_timezone":"Europe/Paris"}}"#
}
