import Foundation

public enum UiLocale: String, CaseIterable, Sendable {
    case english = "en"
    case spanish = "es"
    case french = "fr"
    case portuguese = "pt"

    public var foundationLocale: Locale {
        Locale(identifier: rawValue)
    }

    public static func normalized(_ languageTag: String) -> UiLocale? {
        let normalized = languageTag
            .trimmingCharacters(in: .whitespacesAndNewlines)
            .replacingOccurrences(of: "_", with: "-")
            .lowercased()
        guard !normalized.isEmpty else { return nil }
        if let exact = UiLocale(rawValue: normalized) {
            return exact
        }
        guard let language = normalized.split(separator: "-").first else { return nil }
        return UiLocale(rawValue: String(language))
    }
}

public enum UiLocaleResolver {
    public static func resolve(
        savedUiLocale: String?,
        preferredLanguages: [String] = Locale.preferredLanguages
    ) -> UiLocale {
        if let savedUiLocale, let saved = UiLocale.normalized(savedUiLocale) {
            return saved
        }
        return preferredLanguages.lazy.compactMap(UiLocale.normalized).first ?? .english
    }
}
