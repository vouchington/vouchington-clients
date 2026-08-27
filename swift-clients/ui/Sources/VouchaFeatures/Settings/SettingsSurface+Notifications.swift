import SwiftUI
import VouchaLocalization

extension SettingsSurface {
    var notificationSection: some View {
        section(
            .nativeSwiftSettingsNotifications,
            systemImage: "bell",
            accessibilityIdentifier: "notification-settings-heading",
            isAccessibilityFocusTarget: true
        ) {
            notificationSettingsContent
        }
        .task { await notificationSettingsViewModel.load() }
        .onAppear {
            activateNotificationSettingsAccessibility()
        }
        .onChange(of: notificationActivationToken) { _, _ in activateNotificationSettingsAccessibility() }
    }

    private func activateNotificationSettingsAccessibility() {
        guard focusedSection == .notifications else { return }
        notificationA11yActivator.activate(
            token: notificationActivationToken,
            focus: { isNotificationSettingsHeadingFocused = true },
            announcement: UiMessages.string(.nativeSwiftSettingsNotificationSettingsOpened, locale: nativeUiLocale)
        )
    }

    var notificationSettingsControls: some View {
        Group {
            Toggle(
                UiMessages.string(.nativeSwiftSettingsEngagementEmails, locale: nativeUiLocale),
                isOn: binding(.engagement, get: { notificationSettingsViewModel.current?.engagement ?? true })
            )
            .disabled(notificationSettingsViewModel.isPending(.engagement))
            digestFrequencyPicker(
                .nativeSwiftSettingsNewsDigest,
                field: .newsDigest,
                selection: notificationSettingsViewModel.current?.newsDigest ?? "weekly"
            )
            .disabled(notificationSettingsViewModel.isPending(.newsDigest))
            Toggle(
                UiMessages.string(.nativeSwiftSettingsModerationEmails, locale: nativeUiLocale),
                isOn: binding(.moderation, get: { notificationSettingsViewModel.current?.moderation ?? true })
            )
            .disabled(notificationSettingsViewModel.isPending(.moderation))
            digestFrequencyPicker(
                .nativeSwiftSettingsCommunityDigest,
                field: .communityDigest,
                selection: notificationSettingsViewModel.current?.communityDigest ?? "weekly"
            )
            .disabled(notificationSettingsViewModel.isPending(.communityDigest))

            if notificationSettingsViewModel.shouldShowModerationSchedule {
                Picker(
                    UiMessages.string(.nativeSwiftSettingsModerationCadence, locale: nativeUiLocale),
                    selection: binding(.cadence, get: { notificationSettingsViewModel.current?.cadence ?? "daily" })
                ) {
                    Text(UiMessages.string(.nativeSwiftSettingsDaily, locale: nativeUiLocale)).tag("daily")
                    Text(UiMessages.string(.nativeSwiftSettingsSelectedDays, locale: nativeUiLocale))
                        .tag("selected_days")
                    Text(UiMessages.string(.nativeSwiftSettingsWeekly, locale: nativeUiLocale)).tag("weekly")
                }
                .pickerStyle(.menu)
                .disabled(notificationSettingsViewModel.isPending(.cadence))
                DatePicker(
                    UiMessages.string(.nativeSwiftSettingsModerationTime, locale: nativeUiLocale),
                    selection: timeBinding,
                    displayedComponents: .hourAndMinute
                )
                .disabled(notificationSettingsViewModel.isPending(.time))
                Picker(
                    UiMessages.string(.nativeSwiftSettingsModerationTimezone, locale: nativeUiLocale),
                    selection: binding(
                        .timezone,
                        get: { notificationSettingsViewModel.current?.timezone ?? "America/Los_Angeles" }
                    )
                ) {
                    ForEach(timezones, id: \.self) { Text($0).tag($0) }
                }
                .pickerStyle(.menu)
                .disabled(notificationSettingsViewModel.isPending(.timezone))
                if notificationSettingsViewModel.shouldShowModerationDays {
                    VStack(alignment: .leading, spacing: 8) {
                        Text(UiMessages.string(.nativeSwiftSettingsModerationDays, locale: nativeUiLocale))
                            .font(.subheadline)
                            .foregroundStyle(.secondary)
                        LazyVGrid(columns: [GridItem(.adaptive(minimum: 96), spacing: 8)], spacing: 8) {
                            ForEach(moderationEmailDays, id: \.self) { day in
                                Toggle(localizedWeekday(day), isOn: dayBinding(day))
                                    .disabled(isFinalSelectedDay(day) || notificationSettingsViewModel.isPending(.days))
                            }
                        }
                    }
                }
            }
        }
    }

    private func digestFrequencyPicker(
        _ title: UiMessageKey,
        field: NotificationPreferenceField,
        selection: String
    ) -> some View {
        Picker(UiMessages.string(title, locale: nativeUiLocale), selection: binding(field, get: { selection })) {
            Text(UiMessages.string(.nativeSwiftSettingsOff, locale: nativeUiLocale)).tag("none")
            Text(UiMessages.string(.nativeSwiftSettingsDaily, locale: nativeUiLocale)).tag("daily")
            Text(UiMessages.string(.nativeSwiftSettingsWeekly, locale: nativeUiLocale)).tag("weekly")
        }
        .pickerStyle(.menu)
    }

    private func binding(_ field: NotificationPreferenceField, get: @escaping () -> Bool) -> Binding<Bool> {
        Binding(
            get: get,
            set: { value in
                Task {
                    switch field {
                    case .engagement: await notificationSettingsViewModel.setEngagement(value)
                    case .moderation: await notificationSettingsViewModel.setModeration(value)
                    default: break
                    }
                }
            }
        )
    }

    private func binding(_ field: NotificationPreferenceField, get: @escaping () -> String) -> Binding<String> {
        Binding(
            get: get,
            set: { value in
                Task {
                    switch field {
                    case .newsDigest: await notificationSettingsViewModel.setNewsDigest(value)
                    case .communityDigest: await notificationSettingsViewModel.setCommunityDigest(value)
                    case .cadence: await notificationSettingsViewModel.setCadence(value)
                    case .timezone: await notificationSettingsViewModel.setTimezone(value)
                    default: break
                    }
                }
            }
        )
    }

    private var timeBinding: Binding<Date> {
        Binding(
            get: {
                DateFormatter.hhmm.date(from: notificationSettingsViewModel.current?.time ?? "09:00") ?? Date()
            },
            set: { value in
                Task { await notificationSettingsViewModel.setTime(DateFormatter.hhmm.string(from: value)) }
            }
        )
    }

    private var timezones: [String] {
        Array(Set(TimeZone
                .knownTimeZoneIdentifiers + [notificationSettingsViewModel.current?.timezone ?? "UTC", "UTC"])).sorted()
    }

    private func dayBinding(_ day: Int) -> Binding<Bool> {
        Binding(
            get: { notificationSettingsViewModel.current?.days.contains(day) == true },
            set: { selected in Task { await notificationSettingsViewModel.setDay(day, selected: selected) } }
        )
    }

    private func isFinalSelectedDay(_ day: Int) -> Bool {
        guard let days = notificationSettingsViewModel.current?.days else { return false }
        return days.contains(day) && days.count == 1
    }

    private func localizedWeekday(_ weekday: Int) -> String {
        var calendar = Calendar(identifier: .iso8601)
        calendar.locale = nativeUiLocale
        guard let monday = DateComponents(calendar: calendar, year: 2_024, month: 1, day: 1).date,
              let date = calendar.date(byAdding: .day, value: weekday - 1, to: monday)
        else { return calendar.shortWeekdaySymbols[weekday - 1] }
        let formatter = DateFormatter()
        formatter.calendar = calendar
        formatter.locale = nativeUiLocale
        formatter.dateFormat = "EEE"
        return formatter.string(from: date)
    }
}

private let moderationEmailDays = Array(1 ... 7)

private extension DateFormatter {
    static let hhmm: DateFormatter = { let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.dateFormat = "HH:mm"
        return formatter
    }()
}
