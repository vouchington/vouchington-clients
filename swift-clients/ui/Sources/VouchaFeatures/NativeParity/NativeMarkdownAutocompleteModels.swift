import VouchaModels

struct NativeMarkdownPostSearchResponse: Decodable {
    let results: [NativeMarkdownEntityReference]
    let posts: [String: Post]
}

struct NativeMarkdownEntityReference: Decodable {
    let id: String
    let entityId: String?
}

struct MarkdownAutocompleteSuggestion: Identifiable, Equatable {
    var id: String {
        replacement
    }

    let replacement: String
    let label: String
    let detail: String?
}

struct MarkdownAutocompleteToken: Equatable {
    enum Kind: Character {
        case user = "@"
        case topic = "#"
        case post = "!"
    }

    let kind: Kind
    let query: String
    let range: Range<String.Index>

    static func parse(_ markdown: String) -> MarkdownAutocompleteToken? {
        guard let lastBreak = markdown.lastIndex(where: { $0.isWhitespace || $0.isNewline }) else {
            return parseToken(in: markdown, range: markdown.startIndex ..< markdown.endIndex)
        }
        let start = markdown.index(after: lastBreak)
        return parseToken(in: markdown, range: start ..< markdown.endIndex)
    }

    private static func parseToken(in markdown: String, range: Range<String.Index>) -> MarkdownAutocompleteToken? {
        guard range.lowerBound < range.upperBound else { return nil }
        let marker = markdown[range.lowerBound]
        guard let kind = Kind(rawValue: marker) else { return nil }
        let queryStart = markdown.index(after: range.lowerBound)
        let query = String(markdown[queryStart ..< range.upperBound])
        guard !query.isEmpty else { return nil }
        return .init(kind: kind, query: query, range: range)
    }
}
