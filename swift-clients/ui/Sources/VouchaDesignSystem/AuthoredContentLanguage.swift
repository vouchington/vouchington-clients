import SwiftUI

public enum AuthoredContentLanguage {
    private static let supportedLanguages: Set<String> = [
        "af",
        "sq",
        "ar",
        "hy",
        "az",
        "eu",
        "be",
        "bn",
        "nb",
        "bs",
        "bg",
        "ca",
        "zh",
        "hr",
        "cs",
        "da",
        "nl",
        "en",
        "eo",
        "et",
        "fi",
        "fr",
        "lg",
        "ka",
        "de",
        "el",
        "gu",
        "he",
        "hi",
        "hu",
        "is",
        "id",
        "ga",
        "it",
        "ja",
        "kk",
        "ko",
        "la",
        "lv",
        "lt",
        "mk",
        "ms",
        "mi",
        "mr",
        "mn",
        "nn",
        "fa",
        "pl",
        "pt",
        "pa",
        "ro",
        "ru",
        "sr",
        "sn",
        "sk",
        "sl",
        "so",
        "st",
        "es",
        "sw",
        "sv",
        "tl",
        "ta",
        "te",
        "th",
        "ts",
        "tn",
        "tr",
        "uk",
        "ur",
        "vi",
        "cy",
        "xh",
        "yo",
        "zu"
    ]
    private static let rightToLeftLanguages: Set<String> = ["ar", "fa", "he", "ur"]

    public static func resolved(declared: String?, detected: String?) -> String? {
        for candidate in [declared, detected] {
            guard let candidate else { continue }
            let language = candidate.trimmingCharacters(in: .whitespacesAndNewlines)
                .split(whereSeparator: { $0 == "-" || $0 == "_" }).first.map(String.init)?.lowercased()
            guard let language, supportedLanguages.contains(language) else { continue }
            return language
        }
        return nil
    }

    public static func layoutDirection(declared: String?, detected: String?) -> LayoutDirection? {
        guard let language = resolved(declared: declared, detected: detected) else { return nil }
        return rightToLeftLanguages.contains(language) ? .rightToLeft : .leftToRight
    }
}

public extension View {
    @ViewBuilder
    func authoredContentLanguage(declared: String?, detected: String?) -> some View {
        let language = AuthoredContentLanguage.resolved(declared: declared, detected: detected)
        let direction = AuthoredContentLanguage.layoutDirection(declared: declared, detected: detected)
        if let language, let direction {
            environment(\.locale, Locale(identifier: language))
                .environment(\.layoutDirection, direction)
        } else {
            self
        }
    }
}
