import Foundation
import SwiftSoup

extension NativeHtmlText {
    static func prefixLines(_ value: AttributedString, prefix: String) -> AttributedString {
        var result = AttributedString()
        var remaining = value
        if !remaining.characters.isEmpty {
            result += AttributedString(prefix)
        }
        while let newlineRange = remaining.range(of: "\n") {
            result += AttributedString(remaining[..<newlineRange.upperBound])
            remaining = AttributedString(remaining[newlineRange.upperBound...])
            if !remaining.characters.isEmpty {
                result += AttributedString(prefix)
            }
        }
        result += remaining
        return result
    }

    static func append(_ value: AttributedString, to output: inout AttributedString) {
        output += value
    }

    static func table(_ element: Element, locale: Locale) throws -> AttributedString {
        var output = AttributedString()
        for row in try element.select("tr").array() {
            var rowOutput = AttributedString()
            for cell in row.children().array() where ["th", "td"].contains(cell.tagName().lowercased()) {
                if !rowOutput.characters.isEmpty {
                    append(AttributedString(" | "), to: &rowOutput)
                }
                try append(renderChildren(of: cell, locale: locale), to: &rowOutput)
            }
            if !rowOutput.characters.isEmpty {
                append(rowOutput, to: &output)
                append(AttributedString("\n"), to: &output)
            }
        }
        return withTrailingBlankLine(output)
    }

    static func list(_ element: Element, ordered: Bool, locale: Locale) throws -> AttributedString {
        var output = AttributedString()
        var index = 1
        for child in element.children().array() where child.tagName().lowercased() == "li" {
            append(AttributedString(ordered ? "\(index). " : "- "), to: &output)
            try append(listItemContent(child, locale: locale), to: &output)
            append(AttributedString("\n"), to: &output)
            index += 1
        }
        return withTrailingBlankLine(output)
    }

    private static func listItemContent(_ element: Element, locale: Locale) throws -> AttributedString {
        var output = AttributedString()
        for node in element.getChildNodes() {
            if let child = node as? Element, ["ul", "ol"].contains(child.tagName().lowercased()) {
                if !output.characters.isEmpty {
                    append(AttributedString("\n"), to: &output)
                }
                try append(
                    list(child, ordered: child.tagName().lowercased() == "ol", locale: locale),
                    to: &output
                )
            } else {
                try append(renderNode(node, locale: locale), to: &output)
            }
        }
        return output
    }
}
