import Foundation
import SwiftSoup
import VouchaLocalization

extension NativeHtmlText {
    static func inlineIntent(
        _ element: Element,
        _ intent: InlinePresentationIntent,
        locale: Locale
    ) throws -> AttributedString {
        var value = try renderChildren(of: element, locale: locale)
        for run in value.runs {
            value[run.range].inlinePresentationIntent = (run.inlinePresentationIntent ?? []).union(intent)
        }
        return value
    }

    static func preformatted(_ element: Element) throws -> AttributedString {
        var value = try AttributedString(element.text(trimAndNormaliseWhitespace: false))
        value.inlinePresentationIntent = .code
        return withTrailingBlankLine(value)
    }

    static func quote(_ element: Element, locale: Locale) throws -> AttributedString {
        var value = try renderChildren(of: element, locale: locale)
        value = prefixLines(value, prefix: "> ")
        return withTrailingBlankLine(value)
    }

    static func link(_ element: Element, locale: Locale) throws -> AttributedString {
        var value = try renderChildren(of: element, locale: locale)
        if let href = try? element.attr("href"),
           let url = URL(string: href),
           let scheme = url.scheme?.lowercased(),
           ["http", "https", "mailto"].contains(scheme) {
            value.link = url
        }
        return value
    }

    static func imageAlt(_ element: Element, locale: Locale) -> AttributedString {
        let alt = (try? element.attr("alt")).flatMap { $0.isEmpty ? nil : $0 }
            ?? UiMessages.string(.nativeSwiftPresentationValuesImage, locale: locale)
        return AttributedString("[\(alt)]")
    }

    static func block(_ element: Element, locale: Locale) throws -> AttributedString {
        try withTrailingBlankLine(renderChildren(of: element, locale: locale))
    }
}
