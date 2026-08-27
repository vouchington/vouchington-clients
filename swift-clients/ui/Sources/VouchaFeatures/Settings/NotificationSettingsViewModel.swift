import Foundation
import Observation
import VouchaAPI
import VouchaLocalization
import VouchaModels

public enum NotificationPreferenceField: CaseIterable, Hashable, Sendable {
    case engagement, newsDigest, moderation, communityDigest, cadence, days, time, timezone
}

public struct NotificationSettingsSnapshot: Equatable, Sendable {
    public var engagement: Bool
    public var newsDigest: String
    public var moderation: Bool
    public var communityDigest: String
    public var cadence: String
    public var days: Set<Int>
    public var time: String
    public var timezone: String

    init(_ preferences: EmailPreferences, fallbackTimezone: String) {
        engagement = preferences.engagementEmailsEnabled
        newsDigest = preferences.newsDigestFrequency
        moderation = preferences.moderationEmailsEnabled
        communityDigest = preferences.communityDigestFrequency
        cadence = preferences.moderationEmailCadence
        days = Set(preferences.moderationEmailDaysOfWeek)
        time = preferences.moderationEmailTimeOfDay
        if let savedTimezone = preferences.moderationEmailTimezone, !savedTimezone.isEmpty {
            timezone = savedTimezone
        } else {
            timezone = fallbackTimezone
        }
    }
}

@Observable
@MainActor
public final class NotificationSettingsViewModel {
    public internal(set) var state: LoadState = .idle
    public internal(set) var committed: NotificationSettingsSnapshot?
    public internal(set) var current: NotificationSettingsSnapshot?
    public internal(set) var pendingFields: Set<NotificationPreferenceField> = []
    public internal(set) var statusMessage: UiVerbatimText?
    @ObservationIgnored var generations: [NotificationPreferenceField: Int] = [:]
    @ObservationIgnored var loadGeneration = 0
    @ObservationIgnored var hasRequestedDefaultTimezone = false
    @ObservationIgnored var hasExplicitTimezoneSelection = false
    @ObservationIgnored let client: APIClient?
    @ObservationIgnored let timezoneResolver: @Sendable () -> String?

    public init(
        client: APIClient?,
        timezoneResolver: @escaping @Sendable () -> String? = { TimeZone.current.identifier }
    ) {
        self.client = client
        self.timezoneResolver = timezoneResolver
    }

}
