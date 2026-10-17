import Foundation

public extension UiMessages {
    static func queueDomainCount(_ count: Int, complete: Bool, locale: Locale) -> String {
        number(count, locale: locale) + (complete ? "" : "+")
    }
}
