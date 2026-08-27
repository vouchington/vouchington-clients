import Foundation
import VouchaLocalization

let uiEnglishTestLocale = Locale(identifier: "en")
let uiTestTimeZone = TimeZone(secondsFromGMT: 0)!

func uiEnglish(_ message: UiMessage) -> String {
    UiMessages.string(message, locale: .english, timeZone: uiTestTimeZone)
}

func uiEnglish(_ message: UiMessage?) -> String? {
    message.map(uiEnglish)
}

func uiEnglish(_ key: UiMessageKey) -> String {
    UiMessages.string(key, locale: .english)
}

func uiEnglish(_ key: UiMessageKey?) -> String? {
    key.map(uiEnglish)
}

func uiEnglish(_ text: UiVerbatimText) -> String {
    UiMessages.string(text, locale: .english, timeZone: uiTestTimeZone)
}

func uiEnglish(_ text: UiVerbatimText?) -> String? {
    text.map(uiEnglish)
}

func uiEnglishDate(
    _ value: Date,
    date: Date.FormatStyle.DateStyle = .abbreviated,
    time: Date.FormatStyle.TimeStyle = .shortened
) -> String {
    UiMessages.date(value, date: date, time: time, locale: .english, timeZone: uiTestTimeZone)
}

func normalizedUiText(_ value: String) -> String {
    value
        .replacingOccurrences(of: "\u{00A0}", with: " ")
        .replacingOccurrences(of: "\u{202F}", with: " ")
}
