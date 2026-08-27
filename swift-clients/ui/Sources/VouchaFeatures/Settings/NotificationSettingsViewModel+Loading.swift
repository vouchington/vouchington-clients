import Foundation
import VouchaAPI
import VouchaCore
import VouchaModels

public extension NotificationSettingsViewModel {
    var isPending: Bool {
        !pendingFields.isEmpty
    }

    func isPending(_ field: NotificationPreferenceField) -> Bool {
        pendingFields.contains(field)
    }

    var shouldShowModerationSchedule: Bool {
        current?.moderation == true
    }

    var shouldShowModerationDays: Bool {
        shouldShowModerationSchedule && current?.cadence == "selected_days"
    }

    func load() async {
        guard let client else { return }
        loadGeneration += 1
        let requestGeneration = loadGeneration
        let loadGenerations = generations
        let fieldsPendingAtLoadStart = pendingFields
        state = .loading
        statusMessage = nil
        do {
            let response: EmailPreferencesResponse = try await client.send(.myEmailPreferences)
            guard loadGeneration == requestGeneration else { return }
            let snapshot = NotificationSettingsSnapshot(response.emailPreferences, fallbackTimezone: displayTimezone)
            committed = committed ?? snapshot
            current = current ?? snapshot
            for field in NotificationPreferenceField.allCases
                where !fieldsPendingAtLoadStart.contains(field) && loadGenerations[field] == generations[field] {
                apply(snapshot, field: field, to: &committed)
                apply(snapshot, field: field, to: &current)
            }
            state = .loaded
            await setDefaultTimezoneIfNeeded(
                response.emailPreferences.moderationEmailTimezone,
                loadTimezoneGeneration: loadGenerations[.timezone],
                canSetDefaultTimezone: !fieldsPendingAtLoadStart.contains(.timezone)
            )
        } catch {
            guard loadGeneration == requestGeneration else { return }
            state = .error((error as? VouchaError) ?? .unexpected(error.localizedDescription))
        }
    }

    var displayTimezone: String {
        detectedTimezone ?? "America/Los_Angeles"
    }

    var detectedTimezone: String? {
        let candidate = timezoneResolver()
        guard let candidate, TimeZone(identifier: candidate) != nil else { return nil }
        return candidate
    }

    func setDefaultTimezoneIfNeeded(
        _ savedTimezone: String?,
        loadTimezoneGeneration: Int?,
        canSetDefaultTimezone: Bool
    ) async {
        guard savedTimezone == nil,
              canSetDefaultTimezone,
              generations[.timezone] == loadTimezoneGeneration,
              !hasRequestedDefaultTimezone,
              !hasExplicitTimezoneSelection,
              let detectedTimezone
        else { return }
        hasRequestedDefaultTimezone = true
        let defaultTimezoneWasSaved = await update(.timezone, value: detectedTimezone)
        if !defaultTimezoneWasSaved {
            hasRequestedDefaultTimezone = false
        }
    }
}
