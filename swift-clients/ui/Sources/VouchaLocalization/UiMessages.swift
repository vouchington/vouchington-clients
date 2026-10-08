import Foundation

public enum UiPluralCategory: String, Sendable {
    case one
    case other
}

public enum UiPluralRules {
    public static func cardinalCategory(for value: Int64, locale: UiLocale) -> UiPluralCategory {
        switch locale {
        case .english, .spanish:
            value == 1 ? .one : .other
        case .french, .portuguese:
            value == 0 || value == 1 ? .one : .other
        }
    }

    public static func cardinalCategory(for value: Double, locale: UiLocale) -> UiPluralCategory {
        guard value.isFinite else { return .other }
        let absoluteValue = abs(value)
        let integer = absoluteValue.rounded(.towardZero)
        let isInteger = absoluteValue == integer

        switch locale {
        case .english, .spanish:
            return isInteger && integer == 1 ? .one : .other
        case .french, .portuguese:
            if integer == 0 || integer == 1 {
                return .one
            }
            return .other
        }
    }
}

enum UiMessageResourceKeyError: Error, Equatable {
    case missingValueParameter(UiMessageKey, parameter: String)
    case missingSelectCase(UiMessageKey, parameter: String)
    case invalidSelectCase(UiMessageKey, parameter: String, value: String)
}

public enum UiMessages {
    private static let localizedBundles: [UiLocale: Bundle] = Dictionary(
        uniqueKeysWithValues: UiLocale.allCases.compactMap { locale -> (UiLocale, Bundle)? in
            guard let path = Bundle.module.path(forResource: locale.rawValue, ofType: "lproj"),
                  let bundle = Bundle(path: path)
            else { return nil }
            return (locale, bundle)
        }
    )

    public static func string(
        _ key: UiMessageKey,
        parameters: [String: String] = [:],
        locale: UiLocale
    ) -> String {
        string(UiMessage(key, parameters: parameters), locale: locale)
    }

    public static func string(
        _ text: UiVerbatimText,
        locale: UiLocale,
        timeZone: TimeZone = .current
    ) -> String {
        switch text {
        case let .app(message):
            string(message, locale: locale, timeZone: timeZone)
        case let .composition(parts, separator):
            parts.map { string($0, locale: locale, timeZone: timeZone) }.joined(separator: separator)
        case let .verbatim(value):
            value
        }
    }

    public static func string(
        _ text: UiVerbatimText,
        locale: Locale,
        timeZone: TimeZone = .current
    ) -> String {
        string(text, locale: resolvedLocale(locale), timeZone: timeZone)
    }

    public static func string(
        _ message: UiMessage,
        locale: Locale,
        timeZone: TimeZone = .current
    ) -> String {
        string(message, locale: resolvedLocale(locale), timeZone: timeZone)
    }

    public static func string(
        _ key: UiMessageKey,
        parameters: [String: String] = [:],
        locale: Locale
    ) -> String {
        string(UiMessage(key, parameters: parameters), locale: resolvedLocale(locale))
    }

    public static func string(
        _ message: UiMessage,
        locale: UiLocale,
        timeZone: TimeZone = .current
    ) -> String {
        let resourceKey: String
        do {
            resourceKey = try Self.resourceKey(for: message, locale: locale)
        } catch {
            preconditionFailure("Invalid UI message: \(error)")
        }
        var parameters = message.parameters
        for (name, value) in message.textParameters {
            parameters[name] = string(value, locale: locale, timeZone: timeZone)
        }
        for (name, value) in message.numberParameters {
            parameters[name] = number(value, locale: locale)
        }
        for (name, value) in message.percentParameters {
            parameters[name] = percent(value, locale: locale)
        }
        for (name, parameter) in message.currencyParameters {
            parameters[name] = currency(parameter, locale: locale)
        }
        for (name, parameter) in message.dateParameters {
            parameters[name] = localizedDate(parameter, locale: locale, timeZone: timeZone)
        }
        return replacingTokens(
            in: localizedString(resourceKey, locale: locale),
            parameters: parameters
        )
    }

    static func replacingTokens(in template: String, parameters: [String: String]) -> String {
        var result = ""
        var cursor = template.startIndex

        while cursor < template.endIndex {
            guard template[cursor] == "{",
                  let closingBrace = template[cursor...].firstIndex(of: "}")
            else {
                result.append(template[cursor])
                cursor = template.index(after: cursor)
                continue
            }

            let tokenStart = template.index(after: cursor)
            let token = String(template[tokenStart ..< closingBrace])
            if let replacement = parameters[token], !token.isEmpty {
                result.append(replacement)
                cursor = template.index(after: closingBrace)
            } else {
                result.append(template[cursor])
                cursor = template.index(after: cursor)
            }
        }
        return result
    }

    static func resolvedLocale(_ locale: Locale) -> UiLocale {
        UiLocale.normalized(locale.identifier) ?? .english
    }

    static func resourceKey(for message: UiMessage, locale: UiLocale) throws -> String {
        guard let descriptor = uiMessageDescriptors[message.key] else { return message.key.rawValue }
        guard let value = message.numberParameters[descriptor.valueParameter] else {
            throw UiMessageResourceKeyError.missingValueParameter(
                message.key,
                parameter: descriptor.valueParameter
            )
        }
        let plural = UiPluralRules.cardinalCategory(for: value, locale: locale).rawValue
        switch descriptor.kind {
        case .plural:
            return "\(message.key.rawValue).__plural.\(plural)"
        case .selectPlural:
            let parameter = descriptor.selectParameter ?? "select"
            guard let selectedCase = message.selectedCase else {
                throw UiMessageResourceKeyError.missingSelectCase(message.key, parameter: parameter)
            }
            guard descriptor.cases.contains(selectedCase) else {
                throw UiMessageResourceKeyError.invalidSelectCase(
                    message.key,
                    parameter: parameter,
                    value: selectedCase
                )
            }
            return "\(message.key.rawValue).__select.\(selectedCase).\(plural)"
        }
    }

    static func localizedString(_ key: String, locale: UiLocale) -> String {
        if let overlay = LocalizationValueCache.shared.value(for: key, locale: locale.rawValue) {
            return overlay
        }
        guard let bundle = localizedBundles[locale] else { return key }
        return bundle.localizedString(forKey: key, value: key, table: nil)
    }
}
