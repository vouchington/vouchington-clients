import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct NativeChatGeneralPreferencesFactView: View {
    let preferences: GeneralPreferences
    let locale: Locale

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            row(.nativeSwiftSettingsFollows, value: audience(preferences.followsVisibility.rawValue))
            row(.nativeSwiftSettingsTopicFollows, value: audience(preferences.topicFollowsVisibility.rawValue))
            row(.nativeSwiftSettingsRssFollows, value: audience(preferences.rssFeedFollowsVisibility.rawValue))
            row(
                .nativeSwiftSettingsCommunityMemberships,
                value: audience(preferences.communityMembershipsVisibility.rawValue)
            )
            row(.nativeSwiftSettingsFollowers, value: audience(preferences.followersVisibility.rawValue))
            row(.nativeSwiftSettingsLikes, value: audience(preferences.likesVisibility.rawValue))
            row(.nativeSwiftSettingsDirectMessages, value: audience(preferences.directMessagesAudience.rawValue))
            row(.nativeSwiftSettingsDefaultPostAudience, value: audience(preferences.defaultPostBroadcast.rawValue))
            row(.nativeSwiftSettingsDefaultPostPrivacy, value: .message(
                preferences.defaultPostPrivacy == .public ? .nativeSwiftSettingsPublic : .nativeSwiftSettingsPrivate
            ))
            row(
                .settingsLanguageCountryLabel,
                value: preferences.country.map(UiVerbatimText.protocolValue)
                    ?? .message(.settingsLanguageNoCountryPreference)
            )
            row(
                .settingsLanguageInterfaceLabel,
                value: preferences.uiLocale.map(UiVerbatimText.protocolValue)
                    ?? .message(.settingsLanguageUseSiteDefault)
            )
            row(.extractedMyPreferencesFormHackerNewsDiscussions065664a4, value: .message(
                preferences
                    .shouldImportHackerNewsDiscussions ? .nativeSwiftRouteSurfaceTrue : .nativeSwiftRouteSurfaceFalse
            ))
        }
        .accessibilityIdentifier("native-chat-general-preferences-fact")
    }

    private func row(_ key: UiMessageKey, value: UiVerbatimText) -> some View {
        HStack(alignment: .firstTextBaseline, spacing: Spacing.xs) {
            Text(UiMessages.string(key, locale: locale))
                .foregroundStyle(Colors.secondaryLabel)
            Text(verbatim: UiMessages.string(value, locale: locale))
        }
        .accessibilityElement(children: .combine)
    }

    private func audience(_ value: String) -> UiVerbatimText {
        switch value {
        case "everyone": .message(.nativeSwiftSettingsEveryone)
        case "users": .message(.extractedMyAudienceSelectLoggedInUsers81dac12b)
        case "followers": .message(.nativeSwiftSettingsFollowers)
        case "mutual_followers": .message(.nativeSwiftSettingsMutualFollowers)
        case "nobody": .message(.nativeSwiftPresentationValuesNobody)
        default: .protocolValue("")
        }
    }
}
