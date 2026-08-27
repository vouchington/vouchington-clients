import SwiftSoup
import SwiftUI
import VouchaLocalization

public struct NativeHtmlContent: View {
    @Environment(\.locale)
    private var nativeUiLocale
    private let html: String?
    private let fallback: String?
    private let lineLimit: Int?
    private let font: Font
    private let foregroundStyle: Color?

    public init(
        html: String?,
        fallback: String? = nil,
        lineLimit: Int? = nil,
        font: Font = Typography.body,
        foregroundStyle: Color? = nil
    ) {
        self.html = html
        self.fallback = fallback
        self.lineLimit = lineLimit
        self.font = font
        self.foregroundStyle = foregroundStyle
    }

    public var body: some View {
        let attributed = NativeHtmlText.attributedText(
            html: html,
            fallback: fallback,
            locale: nativeUiLocale
        )
        if !attributed.characters.isEmpty {
            Text(attributed)
                .font(font)
                .foregroundStyle(foregroundStyle ?? Color.primary)
                .lineLimit(lineLimit)
                .fixedSize(horizontal: false, vertical: true)
        }
    }

    public static func plainText(
        html: String?,
        fallback: String? = nil,
        locale: Locale = Locale(identifier: UiLocale.english.rawValue)
    ) -> String {
        NativeHtmlText.plainText(html: html, fallback: fallback, locale: locale)
    }
}

public enum NativeHtmlText {
    private static let blockTags: Set<String> = ["p", "div", "section", "article"]
    private static let headingTags: Set<String> = ["h1", "h2", "h3", "h4", "h5", "h6"]
    private static let unsafeTags: Set<String> = [
        "script",
        "style",
        "iframe",
        "form",
        "input",
        "button",
        "textarea",
        "select"
    ]

    public static func plainText(
        html: String?,
        fallback: String? = nil,
        locale: Locale = Locale(identifier: UiLocale.english.rawValue)
    ) -> String {
        let attributed = attributedText(html: html, fallback: fallback, locale: locale)
        return String(attributed.characters)
    }

    public static func attributedText(
        html: String?,
        fallback: String? = nil,
        locale: Locale = Locale(identifier: UiLocale.english.rawValue)
    ) -> AttributedString {
        let source = html?.trimmingCharacters(in: .whitespacesAndNewlines)
        guard let source, !source.isEmpty else {
            return AttributedString(fallback ?? "")
        }

        do {
            let document = try SwiftSoup.parseBodyFragment(source)
            let rendered = try renderChildren(of: document.body(), locale: locale)
            if rendered.characters.isEmpty, let fallback {
                return AttributedString(fallback)
            }
            return rendered
        } catch {
            return AttributedString(fallback ?? "")
        }
    }

    static func renderChildren(of element: Element?, locale: Locale) throws -> AttributedString {
        var output = AttributedString()
        guard let element else { return output }
        for node in element.getChildNodes() {
            try append(renderNode(node, locale: locale), to: &output)
        }
        return output
    }

    static func renderNode(_ node: Node, locale: Locale) throws -> AttributedString {
        if let text = node as? TextNode {
            return AttributedString(text.text())
        }

        guard let element = node as? Element else { return AttributedString() }
        let tag = element.tagName().lowercased()

        if blockTags.contains(tag) {
            return try block(element, locale: locale)
        }
        if headingTags.contains(tag) {
            var value = try block(element, locale: locale)
            value.inlinePresentationIntent = .stronglyEmphasized
            return value
        }
        if unsafeTags.contains(tag) {
            return AttributedString()
        }

        return try renderKnownElement(element, tag: tag, locale: locale)
    }

    static func withTrailingBlankLine(_ value: AttributedString) -> AttributedString {
        var output = value
        if !output.characters.isEmpty {
            append(AttributedString("\n\n"), to: &output)
        }
        return output
    }
}
