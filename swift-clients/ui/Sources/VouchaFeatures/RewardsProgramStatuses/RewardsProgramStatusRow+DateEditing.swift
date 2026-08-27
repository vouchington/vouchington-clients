import SwiftUI
import VouchaLocalization
import VouchaModels

extension RewardsProgramStatusRow {
    @ViewBuilder
    var dateSummary: some View {
        if let since = status.since {
            Text(
                UiMessages.string(
                    .extractedRewardsProgramStatusesManagerStatusSummarySinceDate4a6fc195,
                    parameters: ["date": formatted(since)],
                    locale: locale
                )
            )
        }
        if let until = status.until {
            Text(
                UiMessages.string(
                    .extractedRewardsProgramStatusesManagerStatusSummaryUntilDate0bce791c,
                    parameters: ["date": formatted(until)],
                    locale: locale
                )
            )
        }
    }

    var dateEditor: some View {
        VStack(alignment: .leading, spacing: 8) {
            optionalDate(
                .extractedRewardsProgramStatusesManagerStatusListSince98af1ed6,
                value: $interactionState.draft.since
            )
            optionalDate(
                .extractedRewardsProgramStatusesManagerStatusListUntil7caf856e,
                value: $interactionState.draft.until
            )
            HStack {
                Button(
                    UiMessages.string(
                        .extractedRewardsProgramStatusesManagerStatusListSave1509f561,
                        locale: locale
                    )
                ) {
                    Task {
                        if await viewModel.save(status: status, draft: interactionState.draft) {
                            interactionState.isEditing = false
                        }
                    }
                }
                .buttonStyle(.borderedProminent)
                .disabled(viewModel.mutatingIds.contains(status.id))
                Button(
                    UiMessages.string(
                        .extractedRewardsProgramStatusesManagerStatusListCancel19766ed6,
                        locale: locale
                    )
                ) {
                    interactionState.draft = RewardsProgramStatusDraft(status: status)
                    interactionState.isEditing = false
                }.disabled(viewModel.mutatingIds.contains(status.id))
            }
        }
    }

    func optionalDate(_ key: UiMessageKey, value: Binding<LocalDate?>) -> some View {
        VStack(alignment: .leading) {
            Toggle(
                UiMessages.string(key, locale: locale),
                isOn: Binding(
                    get: { value.wrappedValue != nil },
                    set: { enabled in
                        value.wrappedValue = enabled ? value.wrappedValue ?? LocalDate.fromDatePickerDate(
                            Date(),
                            timeZone: timeZone
                        ) : nil
                    }
                )
            )
            if value.wrappedValue != nil {
                DatePicker(
                    UiMessages.string(key, locale: locale),
                    selection: statusDatePickerBinding(value, timeZone: timeZone),
                    displayedComponents: .date
                ).labelsHidden()
            }
        }
    }

    func formatted(_ value: LocalDate) -> String {
        UiMessages.date(
            value.utcGregorianDate ?? Date(),
            date: .abbreviated,
            locale: locale,
            timeZone: TimeZone(secondsFromGMT: 0) ?? .current
        )
    }
}

private func statusDatePickerBinding(_ value: Binding<LocalDate?>, timeZone: TimeZone) -> Binding<Date> {
    Binding(
        get: { value.wrappedValue?.datePickerDate(timeZone: timeZone) ?? Date() },
        set: { value.wrappedValue = LocalDate.fromDatePickerDate($0, timeZone: timeZone) }
    )
}

private extension LocalDate {
    var utcGregorianDate: Date? {
        var calendar = Calendar(identifier: .gregorian)
        guard let utc = TimeZone(secondsFromGMT: 0) else { return nil }
        calendar.timeZone = utc
        return calendar.date(from: DateComponents(year: year, month: month, day: day))
    }
}
