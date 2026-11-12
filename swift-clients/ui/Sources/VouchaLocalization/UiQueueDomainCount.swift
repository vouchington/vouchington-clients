import Foundation

public extension UiMessages {
    static func cappedNumber(_ count: Double, capped: Bool, locale: Locale) -> String {
        number(count, locale: locale) + (capped ? "+" : "")
    }

    static func queueDomainCount(_ count: Int, complete: Bool, locale: Locale) -> String {
        cappedNumber(Double(count), capped: !complete, locale: locale)
    }
}
