public struct NativeRouteParityChecker: Sendable {
    public enum Resolution: Hashable, Sendable {
        case included(destination: NativeRouteDestinationIdentifier, family: String, pattern: String)
        case excluded(family: String, reason: String, pattern: String)
        case unmapped(path: String)
    }

    private let entries: [NativeRouteCatalogEntry]

    public init(entries: [NativeRouteCatalogEntry] = NativeRouteCatalog.entries) {
        self.entries = entries
    }

    public func resolution(for webPath: String) -> Resolution {
        guard let (entry, match) = matchingRoute(for: webPath) else {
            return .unmapped(path: normalizeNativeRoutePath(webPath))
        }
        let matchedPattern = match.template

        switch entry.kind {
        case let .included(destination):
            return .included(destination: destination, family: entry.auditFamily, pattern: matchedPattern)
        case let .excluded(metadata):
            return .excluded(family: entry.auditFamily, reason: metadata.reason, pattern: matchedPattern)
        }
    }

    public func destinationIdentifier(for webPath: String) -> NativeRouteDestinationIdentifier? {
        guard case let .included(destination, _, _) = resolution(for: webPath) else {
            return nil
        }
        return destination
    }

    public func isMapped(_ webPath: String) -> Bool {
        destinationIdentifier(for: webPath) != nil
    }

    private func matchingRoute(for webPath: String) -> (entry: NativeRouteCatalogEntry, match: NativeRouteMatch)? {
        for entry in entries {
            if let match = entry.match(webPath) {
                return (entry, match)
            }
        }
        return nil
    }
}
