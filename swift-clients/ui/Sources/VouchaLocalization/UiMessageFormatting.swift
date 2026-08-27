import Foundation

public extension UiMessages {
    static func number(_ value: Int64, locale: UiLocale) -> String {
        number(value, locale: locale.foundationLocale)
    }

    static func number(_ value: Int64, locale: Locale) -> String {
        value.formatted(.number.locale(resolvedLocale(locale).foundationLocale))
    }

    static func number(_ value: Int, locale: UiLocale) -> String {
        number(value, locale: locale.foundationLocale)
    }

    static func number(_ value: Int, locale: Locale) -> String {
        value.formatted(.number.locale(resolvedLocale(locale).foundationLocale))
    }

    static func integer(
        _ value: Int,
        minimumIntegerDigits: Int,
        locale: UiLocale
    ) -> String {
        integer(value, minimumIntegerDigits: minimumIntegerDigits, locale: locale.foundationLocale)
    }

    static func integer(
        _ value: Int,
        minimumIntegerDigits: Int,
        locale: Locale
    ) -> String {
        let formatter = NumberFormatter()
        formatter.locale = resolvedLocale(locale).foundationLocale
        formatter.numberStyle = .decimal
        formatter.usesGroupingSeparator = false
        formatter.minimumIntegerDigits = minimumIntegerDigits
        return formatter.string(from: NSNumber(value: value)) ?? String(value)
    }

    static func number(_ value: Double, maximumFractionDigits: Int = 2, locale: UiLocale) -> String {
        number(value, maximumFractionDigits: maximumFractionDigits, locale: locale.foundationLocale)
    }

    static func number(_ value: Double, maximumFractionDigits: Int = 2, locale: Locale) -> String {
        value.formatted(
            .number
                .precision(.fractionLength(0 ... maximumFractionDigits))
                .locale(resolvedLocale(locale).foundationLocale)
        )
    }

    static func plural(_ key: UiMessageKey, value: Int64, locale: Locale) -> String {
        let resolvedLocale = resolvedLocale(locale)
        guard let descriptor = uiMessageDescriptors[key], descriptor.kind == .plural else {
            preconditionFailure("Expected plural UI message: \(key.rawValue)")
        }
        let category = UiPluralRules.cardinalCategory(for: value, locale: resolvedLocale).rawValue
        return replacingTokens(
            in: localizedString("\(key.rawValue).__plural.\(category)", locale: resolvedLocale),
            parameters: [descriptor.valueParameter: number(value, locale: resolvedLocale)]
        )
    }

    static func percent(_ value: Double, maximumFractionDigits: Int = 0, locale: UiLocale) -> String {
        percent(value, maximumFractionDigits: maximumFractionDigits, locale: locale.foundationLocale)
    }

    static func percent(_ value: Double, maximumFractionDigits: Int = 0, locale: Locale) -> String {
        value.formatted(
            .percent
                .precision(.fractionLength(0 ... maximumFractionDigits))
                .locale(resolvedLocale(locale).foundationLocale)
        )
    }

    static func currency(_ value: Decimal, code: String, locale: UiLocale) -> String {
        currency(value, code: code, locale: locale.foundationLocale)
    }

    static func currency(_ value: Decimal, code: String, locale: Locale) -> String {
        value.formatted(.currency(code: code).locale(resolvedLocale(locale).foundationLocale))
    }

    static func currency(_ parameter: UiMessageCurrencyParameter, locale: UiLocale) -> String {
        if let maximumFractionDigits = parameter.maximumFractionDigits {
            return currency(
                parameter.value,
                code: parameter.code,
                maximumFractionDigits: maximumFractionDigits,
                locale: locale.foundationLocale
            )
        }
        return currency(parameter.value, code: parameter.code, locale: locale)
    }

    static func currency(
        _ value: Decimal,
        code: String,
        maximumFractionDigits: Int,
        locale: Locale
    ) -> String {
        value.formatted(
            .currency(code: code)
                .precision(.fractionLength(0 ... maximumFractionDigits))
                .locale(resolvedLocale(locale).foundationLocale)
        )
    }

    static func date(
        _ value: Date,
        date: Date.FormatStyle.DateStyle = .abbreviated,
        time: Date.FormatStyle.TimeStyle = .omitted,
        locale: UiLocale,
        timeZone: TimeZone
    ) -> String {
        value.formatted(
            Date.FormatStyle(
                date: date,
                time: time,
                locale: locale.foundationLocale,
                timeZone: timeZone
            )
        )
    }

    static func date(
        _ value: Date,
        date: Date.FormatStyle.DateStyle = .abbreviated,
        time: Date.FormatStyle.TimeStyle = .omitted,
        locale: Locale,
        timeZone: TimeZone
    ) -> String {
        self.date(value, date: date, time: time, locale: resolvedLocale(locale), timeZone: timeZone)
    }
}
