import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct NativeChatEmailPreferencesFactView: View {
    let preferences: EmailPreferences
    let locale: Locale

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            row(.nativeSwiftSettingsEngagementEmails, value: yesNo(preferences.engagementEmailsEnabled))
            row(.nativeSwiftSettingsNewsDigest, value: frequency(preferences.newsDigestFrequency))
            row(.nativeSwiftSettingsModerationEmails, value: yesNo(preferences.moderationEmailsEnabled))
            row(.nativeSwiftSettingsCommunityDigest, value: frequency(preferences.communityDigestFrequency))
            row(.nativeSwiftSettingsModerationCadence, value: cadence(preferences.moderationEmailCadence))
            row(.nativeSwiftSettingsModerationDays, value: days(preferences.moderationEmailDaysOfWeek))
            row(.nativeSwiftSettingsModerationTime, value: .protocolValue(preferences.moderationEmailTimeOfDay))
            if let timezone = preferences.moderationEmailTimezone {
                row(.nativeSwiftSettingsModerationTimezone, value: .protocolValue(timezone))
            }
        }
        .accessibilityIdentifier("native-chat-email-preferences-fact")
    }

    private func row(_ key: UiMessageKey, value: UiVerbatimText) -> some View {
        HStack(alignment: .firstTextBaseline, spacing: Spacing.xs) {
            Text(UiMessages.string(key, locale: locale))
                .foregroundStyle(Colors.secondaryLabel)
            Text(verbatim: UiMessages.string(value, locale: locale))
        }
        .accessibilityElement(children: .combine)
    }

    private func yesNo(_ value: Bool) -> UiVerbatimText {
        .message(value ? .nativeSwiftRouteSurfaceTrue : .nativeSwiftRouteSurfaceFalse)
    }

    private func frequency(_ value: String) -> UiVerbatimText {
        switch value {
        case "none": .message(.nativeSwiftSettingsOff)
        case "daily": .message(.nativeSwiftSettingsDaily)
        case "weekly": .message(.nativeSwiftSettingsWeekly)
        default: .protocolValue("")
        }
    }

    private func cadence(_ value: String) -> UiVerbatimText {
        switch value {
        case "daily": .message(.nativeSwiftSettingsDaily)
        case "selected_days": .message(.nativeSwiftSettingsSelectedDays)
        case "weekly": .message(.nativeSwiftSettingsWeekly)
        default: .protocolValue("")
        }
    }

    private func days(_ values: [Int]) -> UiVerbatimText {
        .joined(values.sorted().map { .protocolValue(weekdayName($0)) })
    }

    private func weekdayName(_ weekday: Int) -> String {
        var calendar = Calendar(identifier: .iso8601)
        calendar.locale = locale
        guard (1 ... 7).contains(weekday),
              let monday = DateComponents(calendar: calendar, year: 2_024, month: 1, day: 1).date,
              let date = calendar.date(byAdding: .day, value: weekday - 1, to: monday) else {
            return ""
        }
        let formatter = DateFormatter()
        formatter.calendar = calendar
        formatter.locale = locale
        formatter.dateFormat = "EEE"
        return formatter.string(from: date)
    }
}
