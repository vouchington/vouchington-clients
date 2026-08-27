import Foundation

public enum HnDiscussionURLCollector {
    public static let limit = 3

    public static func normalize(_ raw: String) -> String? {
        guard var components = URLComponents(string: raw),
              let scheme = components.scheme?.lowercased(),
              scheme == "http" || scheme == "https"
        else {
            return nil
        }
        components.fragment = nil
        components.host = components.host?.lowercased()
        components.queryItems = components.queryItems?.filter { item in
            !item.name.lowercased().hasPrefix("utm_")
        }
        if components.path.count > 1, components.path.hasSuffix("/") {
            components.path = String(components.path.dropLast())
        }
        return components.string
    }

    public static func collect(_ urls: [String]) -> [String] {
        var seen = Set<String>()
        var collected: [String] = []
        for url in urls {
            guard let normalized = normalize(url), seen.insert(normalized).inserted else { continue }
            collected.append(url)
            if collected.count == limit {
                break
            }
        }
        return collected
    }

    public static func extract(fromMarkdown markdown: String?) -> [String] {
        guard let markdown else { return [] }
        let pattern = #"https?://[^\s)\]>"]+"#
        guard let regex = try? NSRegularExpression(pattern: pattern) else { return [] }
        let range = NSRange(markdown.startIndex..., in: markdown)
        let matches = regex.matches(in: markdown, range: range).compactMap { match -> String? in
            Range(match.range, in: markdown).map { value in
                String(markdown[value]).trimmingCharacters(in: CharacterSet(charactersIn: ".,;:!?"))
            }
        }
        return collect(matches)
    }
}
