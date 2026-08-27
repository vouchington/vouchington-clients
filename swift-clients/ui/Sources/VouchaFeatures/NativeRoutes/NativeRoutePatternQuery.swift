import Foundation

public func nativeAppLinkRoutePathAndQuery(_ url: URL) -> String {
    let components = URLComponents(url: url, resolvingAgainstBaseURL: false)
    let encodedPath = components?.percentEncodedPath ?? url.path(percentEncoded: true)
    let path: String = if url.scheme?.caseInsensitiveCompare("voucha") == .orderedSame,
                          let host = url.host, !host.isEmpty {
        "/" + host + encodedPath
    } else {
        encodedPath.isEmpty ? "/" : encodedPath
    }
    guard let query = nativeAppLinkRouteQuery(url), !query.isEmpty else { return path }
    return "\(path)?\(query)"
}

public func nativeAppLinkRouteQuery(_ url: URL) -> String? {
    URLComponents(url: url, resolvingAgainstBaseURL: false)?.percentEncodedQuery
}

func nativeRouteQueryItems(from path: String) -> [String: String] {
    guard let queryStart = path.firstIndex(of: "?") else { return [:] }
    let query = path[path.index(after: queryStart)...].prefix { $0 != "#" }
    return nativeRouteQueryItems(fromEncodedQuery: String(query))
}

func nativeRouteQueryItems(fromEncodedQuery query: String?) -> [String: String] {
    guard let query else { return [:] }
    return Dictionary(query.split(separator: "&").compactMap { item in
        let components = item.split(separator: "=", maxSplits: 1, omittingEmptySubsequences: false)
        guard components.count == 2 else { return nil }
        return (
            decodedNativeRouteQueryComponent(String(components[0])),
            decodedNativeRouteQueryComponent(String(components[1]))
        )
    }, uniquingKeysWith: { first, _ in first })
}
