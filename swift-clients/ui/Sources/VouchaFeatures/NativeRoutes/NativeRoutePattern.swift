import Foundation

public struct NativeRoutePattern: Hashable, Sendable, ExpressibleByStringLiteral {
    public let template: String

    public init(_ template: String) {
        self.template = template
    }

    public init(stringLiteral value: StringLiteralType) {
        template = value
    }

    public func matches(_ webPath: String) -> Bool {
        match(webPath) != nil
    }

    public func match(_ webPath: String) -> NativeRouteMatch? {
        let normalizedTemplate = normalizeNativeRoutePath(template)
        let normalizedPath = normalizeNativeRoutePath(webPath)
        let queryItems = nativeRouteQueryItems(from: webPath)

        let templateSegments = normalizedTemplate.routeSegments
        let pathSegments = normalizedPath.routeSegments

        var pathIndex = 0
        var params: [String: String] = [:]

        for templateSegment in templateSegments {
            if templateSegment == "**" {
                params["splat"] = decodedNativeRouteCapture(pathSegments[pathIndex...].joined(separator: "/"))
                return NativeRouteMatch(
                    path: normalizedPath,
                    template: normalizedTemplate,
                    params: params,
                    queryItems: queryItems
                )
            }

            guard pathIndex < pathSegments.count else {
                return nil
            }

            guard let matchedParams = matchSegmentPattern(templateSegment, against: pathSegments[pathIndex]) else {
                return nil
            }

            params.merge(matchedParams) { _, new in new }
            pathIndex += 1
        }

        guard pathIndex == pathSegments.count else { return nil }
        return NativeRouteMatch(
            path: normalizedPath,
            template: normalizedTemplate,
            params: params,
            queryItems: queryItems
        )
    }
}

public struct NativeRouteMatch: Hashable, Sendable {
    public let path: String
    public let template: String
    public let params: [String: String]
    public let queryItems: [String: String]

    public init(
        path: String,
        template: String,
        params: [String: String] = [:],
        queryItems: [String: String] = [:]
    ) {
        self.path = path
        self.template = template
        self.params = params
        self.queryItems = queryItems
    }

    public func param(_ names: String...) -> String? {
        for name in names {
            if let value = params[name] {
                return value
            }
        }
        return nil
    }

    public func queryValue(_ names: String...) -> String? {
        for name in names {
            if let value = queryItems[name] {
                return value
            }
        }
        return nil
    }

    public var requiresFediverseFeature: Bool {
        path.hasPrefix("/instance/")
    }
}

private func matchSegmentPattern(_ pattern: String, against value: String) -> [String: String]? {
    if pattern.hasPrefix(":") {
        let placeholderContent = pattern.dropFirst()
        if !placeholderContent.isEmpty, placeholderContent.allSatisfy(isPlaceholderNameCharacter) {
            return value.isEmpty ? nil : [String(placeholderContent): decodedNativeRouteCapture(value)]
        }
    }

    guard pattern.contains(":") else {
        return pattern == value ? [:] : nil
    }

    var regex = "^"
    var index = pattern.startIndex
    var placeholderNames: [String] = []

    while index < pattern.endIndex {
        let character = pattern[index]

        if character == ":" {
            regex += "([^/]+)"
            index = pattern.index(after: index)

            let nameStart = index
            while index < pattern.endIndex, isPlaceholderNameCharacter(pattern[index]) {
                index = pattern.index(after: index)
            }
            placeholderNames.append(String(pattern[nameStart ..< index]))
            continue
        }

        regex += NSRegularExpression.escapedPattern(for: String(character))
        index = pattern.index(after: index)
    }

    regex += "$"

    guard let expression = try? NSRegularExpression(pattern: regex) else {
        return nil
    }

    let range = NSRange(value.startIndex ..< value.endIndex, in: value)
    guard let match = expression.firstMatch(in: value, range: range) else {
        return nil
    }

    var params: [String: String] = [:]
    for (offset, name) in placeholderNames.enumerated() {
        let matchRange = match.range(at: offset + 1)
        guard let range = Range(matchRange, in: value) else { continue }
        params[name] = decodedNativeRouteCapture(String(value[range]))
    }
    return params
}

private func isPlaceholderNameCharacter(_ character: Character) -> Bool {
    character.isLetter || character.isNumber || character == "_"
}

private extension String {
    var routeSegments: [String] {
        split(separator: "/", omittingEmptySubsequences: true).map(String.init)
    }
}

func normalizeNativeRoutePath(_ path: String) -> String {
    var normalized = path.trimmingCharacters(in: .whitespacesAndNewlines)

    if let queryIndex = normalized.firstIndex(where: { $0 == "?" || $0 == "#" }) {
        normalized = String(normalized[..<queryIndex])
    }

    if normalized.isEmpty {
        return "/"
    }

    if !normalized.hasPrefix("/") {
        normalized = "/" + normalized
    }

    while normalized.count > 1, normalized.hasSuffix("/") {
        normalized.removeLast()
    }

    return normalized
}
