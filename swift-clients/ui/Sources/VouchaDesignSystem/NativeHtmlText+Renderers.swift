import Foundation
import SwiftSoup

extension NativeHtmlText {
    static func renderKnownElement(
        _ element: Element,
        tag: String,
        locale: Locale
    ) throws -> AttributedString {
        switch tag {
        case "br":
            AttributedString("\n")
        case "hr":
            AttributedString("\n---\n")
        case "strong", "b":
            try inlineIntent(element, .stronglyEmphasized, locale: locale)
        case "em", "i":
            try inlineIntent(element, .emphasized, locale: locale)
        case "code":
            try inlineIntent(element, .code, locale: locale)
        case "pre":
            try preformatted(element)
        case "blockquote":
            try quote(element, locale: locale)
        default:
            try renderContainerElement(element, tag: tag, locale: locale)
        }
    }

    private static func renderContainerElement(
        _ element: Element,
        tag: String,
        locale: Locale
    ) throws -> AttributedString {
        switch tag {
        case "ul":
            try list(element, ordered: false, locale: locale)
        case "ol":
            try list(element, ordered: true, locale: locale)
        case "li":
            try renderChildren(of: element, locale: locale)
        case "table":
            try table(element, locale: locale)
        case "a":
            try link(element, locale: locale)
        case "img":
            imageAlt(element, locale: locale)
        default:
            try renderChildren(of: element, locale: locale)
        }
    }
}
