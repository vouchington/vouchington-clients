import SwiftUI
import VouchaLocalization
import VouchaModels

struct PaymentCardEditor: View {
    @Environment(\.locale)
    private var locale
    @Environment(\.timeZone)
    private var timeZone
    @Binding
    var draft: PaymentCardDraft
    let parentChoices: [PaymentCard]
    let canLoadMoreParents: Bool
    let isSaving: Bool
    let loadMoreParents: () -> Void
    let save: () -> Void
    let cancel: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            optionalDate(.nativeSwiftHouseholdsBookmarksPaymentCardsOpenedOn, value: $draft.openedOn)
            optionalDate(.nativeSwiftHouseholdsBookmarksPaymentCardsClosedOn, value: $draft.closedOn)
            optionalDate(
                .nativeSwiftHouseholdsBookmarksPaymentCardsSignUpBonusOn,
                value: $draft.receivedSignUpBonusOn
            )
            TextField(
                UiMessages.string(.nativeSwiftHouseholdsBookmarksPaymentCardsCreditLimit, locale: locale),
                text: $draft.creditLimitText
            )
            Picker(
                UiMessages.string(.nativeSwiftCommonCurrency, locale: locale),
                selection: $draft.creditLimitCurrency
            ) {
                ForEach(Currency.supported) { currency in
                    Text(currency.code.uppercased()).tag(currency.code)
                }
            }
            Toggle(
                UiMessages.string(.nativeSwiftHouseholdsBookmarksPaymentCardsAuthorizedUser, locale: locale),
                isOn: $draft.isAuthorizedUser
            )
            if draft.isAuthorizedUser {
                Picker(
                    UiMessages.string(.nativeSwiftHouseholdsBookmarksPaymentCardsAuthorizedUserOf, locale: locale),
                    selection: $draft.authorizedUserOfId
                ) {
                    Text(UiMessages.string(.nativeSwiftHouseholdsBookmarksPaymentCardsNone, locale: locale))
                        .tag(String?.none)
                    ForEach(parentChoices) { parent in
                        Text(verbatim: UiMessages.string(.userContent(parent.card.name), locale: locale))
                            .tag(Optional(parent.id))
                    }
                }
                if canLoadMoreParents {
                    Button(UiMessages.string(.nativeSwiftCommonLoadMore, locale: locale), action: loadMoreParents)
                }
            }
            TextField(
                UiMessages.string(.nativeSwiftHouseholdsBookmarksPaymentCardsNote, locale: locale),
                text: $draft.note,
                axis: .vertical
            )
            HStack {
                Button(UiMessages.string(.nativeSwiftHouseholdsBookmarksPaymentCardsSave, locale: locale), action: save)
                    .buttonStyle(.borderedProminent)
                    .disabled(isSaving)
                Button(UiMessages.string(.nativeSwiftCommonCancel, locale: locale), action: cancel)
                    .disabled(isSaving)
            }
        }
    }

    private func optionalDate(_ key: UiMessageKey, value: Binding<LocalDate?>) -> some View {
        VStack(alignment: .leading) {
            Toggle(
                UiMessages.string(key, locale: locale),
                isOn: Binding(
                    get: { value.wrappedValue != nil },
                    set: { enabled in
                        if enabled, value.wrappedValue == nil {
                            value.wrappedValue = LocalDate.fromDatePickerDate(Date(), timeZone: timeZone)
                        } else if !enabled {
                            value.wrappedValue = nil
                        }
                    }
                )
            )
            if value.wrappedValue != nil {
                DatePicker(
                    UiMessages.string(key, locale: locale),
                    selection: paymentCardDatePickerBinding(value, timeZone: timeZone),
                    displayedComponents: .date
                )
                .labelsHidden()
            }
        }
    }
}

func paymentCardDatePickerBinding(
    _ value: Binding<LocalDate?>, timeZone: TimeZone
) -> Binding<Date> {
    Binding(
        get: { value.wrappedValue?.datePickerDate(timeZone: timeZone) ?? Date() },
        set: { value.wrappedValue = LocalDate.fromDatePickerDate($0, timeZone: timeZone) }
    )
}

extension LocalDate {
    static func fromDatePickerDate(_ date: Date, timeZone: TimeZone) -> LocalDate? {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = timeZone
        return LocalDate(
            year: calendar.component(.year, from: date),
            month: calendar.component(.month, from: date),
            day: calendar.component(.day, from: date)
        )
    }

    func datePickerDate(timeZone: TimeZone) -> Date? {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = timeZone
        return calendar.date(from: DateComponents(year: year, month: month, day: day))
    }
}
