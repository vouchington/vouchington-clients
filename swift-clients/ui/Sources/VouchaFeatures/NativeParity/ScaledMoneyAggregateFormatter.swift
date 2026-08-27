import Foundation
import VouchaLocalization
import VouchaModels

enum ScaledMoneyAggregateFormatter {
    static func format(_ value: ScaledMoneyAggregate, locale: Locale) -> UiVerbatimText {
        let components = value.majorUnitString.split(separator: ".", maxSplits: 1)
        let formatter = NumberFormatter()
        formatter.locale = currencyLocale(for: locale)
        formatter.numberStyle = .currency
        formatter.currencyCode = value.currency.uppercased()
        let groupedWhole = grouped(
            String(components[0]),
            separator: formatter.groupingSeparator ?? ",",
            size: max(3, formatter.groupingSize)
        )
        let fraction = components.count == 2 ? (formatter.decimalSeparator ?? ".") + components[1] : ""
        let formatted = "\(formatter.positivePrefix ?? "")\(groupedWhole)\(fraction)\(formatter.positiveSuffix ?? "")"
        return .verbatim(formatted)
    }

    private static func currencyLocale(for locale: Locale) -> Locale {
        let language = locale.language.languageCode?.identifier.split(whereSeparator: { $0 == "-" || $0 == "_" }).first
        return switch language {
        case "en": Locale(identifier: "en_US")
        case "es": Locale(identifier: "es_ES")
        case "fr": Locale(identifier: "fr_FR")
        default: locale
        }
    }

    private static func grouped(_ whole: String, separator: String, size: Int) -> String {
        var groups: [Substring] = []
        var end = whole.endIndex
        while end > whole.startIndex {
            let start = whole.index(end, offsetBy: -min(size, whole.distance(from: whole.startIndex, to: end)))
            groups.append(whole[start ..< end])
            end = start
        }
        return groups.reversed().joined(separator: separator)
    }
}
