import Foundation

extension UiMessages {
    static func localizedDate(
        _ parameter: UiMessageDateParameter,
        locale: UiLocale,
        timeZone: TimeZone
    ) -> String {
        if parameter.dateStyle == .monthYear {
            let formatter = DateFormatter()
            formatter.locale = locale.foundationLocale
            formatter.timeZone = timeZone
            formatter.setLocalizedDateFormatFromTemplate("yMMMM")
            return formatter.string(from: parameter.value)
        }
        return date(
            parameter.value,
            date: parameter.dateStyle.foundationStyle,
            time: parameter.timeStyle.foundationStyle,
            locale: locale,
            timeZone: timeZone
        )
    }
}
