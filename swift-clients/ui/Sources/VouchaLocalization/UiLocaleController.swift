import Foundation
import Observation

@Observable
@MainActor
public final class UiLocaleController {
    public private(set) var locale: UiLocale
    private var savedUiLocale: String?
    private var preferredLanguages: [String]

    public init(
        savedUiLocale: String? = nil,
        preferredLanguages: [String] = Locale.preferredLanguages
    ) {
        self.savedUiLocale = savedUiLocale
        self.preferredLanguages = preferredLanguages
        locale = UiLocaleResolver.resolve(
            savedUiLocale: savedUiLocale,
            preferredLanguages: preferredLanguages
        )
    }

    public func update(savedUiLocale: String?) {
        self.savedUiLocale = savedUiLocale
        updateResolvedLocale()
    }

    public func updatePreferredLanguages(_ preferredLanguages: [String]) {
        self.preferredLanguages = preferredLanguages
        updateResolvedLocale()
    }

    private func updateResolvedLocale() {
        let resolvedLocale = UiLocaleResolver.resolve(
            savedUiLocale: savedUiLocale,
            preferredLanguages: preferredLanguages
        )
        guard locale != resolvedLocale else { return }
        locale = resolvedLocale
    }

    public func string(_ key: UiMessageKey, parameters: [String: String] = [:]) -> String {
        string(UiMessage(key, parameters: parameters))
    }

    public func string(_ message: UiMessage) -> String {
        UiMessages.string(message, locale: locale)
    }

    public func string(_ text: UiVerbatimText) -> String {
        UiMessages.string(text, locale: locale)
    }

    public func number(_ value: Int) -> String {
        UiMessages.number(value, locale: locale)
    }

    public func number(_ value: Double, maximumFractionDigits: Int = 2) -> String {
        UiMessages.number(value, maximumFractionDigits: maximumFractionDigits, locale: locale)
    }

    public func percent(_ value: Double) -> String {
        UiMessages.percent(value, locale: locale)
    }

    public func currency(_ value: Decimal, code: String) -> String {
        UiMessages.currency(value, code: code, locale: locale)
    }

    public func date(
        _ value: Date,
        date: Date.FormatStyle.DateStyle = .abbreviated,
        time: Date.FormatStyle.TimeStyle = .omitted,
        timeZone: TimeZone = .current
    ) -> String {
        UiMessages.date(value, date: date, time: time, locale: locale, timeZone: timeZone)
    }
}
