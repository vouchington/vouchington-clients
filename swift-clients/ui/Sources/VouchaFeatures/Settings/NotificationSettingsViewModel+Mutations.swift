import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

public extension NotificationSettingsViewModel {
    func setEngagement(_ value: Bool) async {
        await update(.engagement, value: value)
    }

    func setNewsDigest(_ value: String) async {
        await update(.newsDigest, value: value)
    }

    func setModeration(_ value: Bool) async {
        await update(.moderation, value: value)
    }

    func setCommunityDigest(_ value: String) async {
        await update(.communityDigest, value: value)
    }

    func setCadence(_ value: String) async {
        await update(.cadence, value: value)
    }

    func setTime(_ value: String) async {
        await update(.time, value: value)
    }

    func setTimezone(_ value: String) async {
        hasExplicitTimezoneSelection = true
        _ = await update(.timezone, value: value)
    }

    func setDay(_ day: Int, selected: Bool) async {
        guard var snapshot = current else { return }
        if selected {
            snapshot.days.insert(day)
        } else {
            guard snapshot.days.count > 1 else { return }
            snapshot.days.remove(day)
        }
        await update(.days, snapshot: snapshot)
    }

    func update(_ field: NotificationPreferenceField, value: Bool) async {
        guard var snapshot = current else { return }
        switch field {
        case .engagement: snapshot.engagement = value
        case .moderation: snapshot.moderation = value
        default: return
        }
        await update(field, snapshot: snapshot)
    }

    @discardableResult
    func update(_ field: NotificationPreferenceField, value: String) async -> Bool {
        guard var snapshot = current else { return false }
        switch field {
        case .newsDigest: snapshot.newsDigest = value
        case .communityDigest: snapshot.communityDigest = value
        case .cadence: snapshot.cadence = value
        case .time: snapshot.time = value
        case .timezone: snapshot.timezone = value
        default: return false
        }
        return await update(field, snapshot: snapshot)
    }

    @discardableResult
    func update(_ field: NotificationPreferenceField, snapshot: NotificationSettingsSnapshot) async -> Bool {
        guard let client, !pendingFields.contains(field), let previous = current else { return false }
        let generation = (generations[field] ?? 0) + 1
        generations[field] = generation
        pendingFields.insert(field)
        current = snapshot
        statusMessage = nil
        do {
            let response: EmailPreferencesResponse = try await client.send(endpoint(field, snapshot: snapshot))
            guard generations[field] == generation else { return false }
            let returned = NotificationSettingsSnapshot(response.emailPreferences, fallbackTimezone: displayTimezone)
            apply(returned, field: field, to: &committed)
            apply(returned, field: field, to: &current)
            pendingFields.remove(field)
            state = .loaded
            return true
        } catch {
            guard generations[field] == generation else { return false }
            apply(previous, field: field, to: &current)
            pendingFields.remove(field)
            statusMessage = .verbatim((error as? VouchaError)?.errorDescription ?? error.localizedDescription)
            state = .loaded
            return false
        }
    }

    func apply(
        _ source: NotificationSettingsSnapshot,
        field: NotificationPreferenceField,
        to target: inout NotificationSettingsSnapshot?
    ) {
        guard var updated = target else { return }
        switch field {
        case .engagement: updated.engagement = source.engagement
        case .newsDigest: updated.newsDigest = source.newsDigest
        case .moderation: updated.moderation = source.moderation
        case .communityDigest: updated.communityDigest = source.communityDigest
        case .cadence: updated.cadence = source.cadence
        case .days: updated.days = source.days
        case .time: updated.time = source.time
        case .timezone: updated.timezone = source.timezone
        }
        target = updated
    }

    private func endpoint(_ field: NotificationPreferenceField, snapshot: NotificationSettingsSnapshot) -> Endpoint {
        switch field {
        case .engagement: .updateMyEmailPreferences(engagementEmailsEnabled: snapshot.engagement)
        case .newsDigest: .updateMyEmailPreferences(newsDigestFrequency: snapshot.newsDigest)
        case .moderation: .updateMyEmailPreferences(moderationEmailsEnabled: snapshot.moderation)
        case .communityDigest: .updateMyEmailPreferences(communityDigestFrequency: snapshot.communityDigest)
        case .cadence: .updateMyEmailPreferences(moderationEmailCadence: snapshot.cadence)
        case .days: .updateMyEmailPreferences(moderationEmailDaysOfWeek: snapshot.days.sorted())
        case .time: .updateMyEmailPreferences(moderationEmailTimeOfDay: snapshot.time)
        case .timezone: .updateMyEmailPreferences(moderationEmailTimezone: snapshot.timezone)
        }
    }
}
