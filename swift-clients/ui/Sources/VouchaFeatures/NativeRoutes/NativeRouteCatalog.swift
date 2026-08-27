import VouchaLocalization

public struct NativeRouteExclusionAuditMetadata: Hashable, Sendable {
    public let family: String
    public let reason: String

    public init(family: String, reason: String) {
        self.family = family
        self.reason = reason
    }
}

public struct NativeRouteCatalogEntry: Hashable, Sendable {
    public enum Kind: Hashable, Sendable {
        case included(NativeRouteDestinationIdentifier)
        case excluded(NativeRouteExclusionAuditMetadata)
    }

    public let representativePath: String
    public let patterns: [NativeRoutePattern]
    public let kind: Kind

    public init(
        representativePath: String,
        patterns: [NativeRoutePattern],
        kind: Kind
    ) {
        self.representativePath = representativePath
        self.patterns = patterns
        self.kind = kind
    }

    public var destinationIdentifier: NativeRouteDestinationIdentifier? {
        guard case let .included(destination) = kind else {
            return nil
        }
        return destination
    }

    public var exclusionReason: String? {
        exclusionAuditMetadata?.reason
    }

    public var exclusionAuditMetadata: NativeRouteExclusionAuditMetadata? {
        guard case let .excluded(metadata) = kind else { return nil }
        return metadata
    }

    public var auditFamily: String {
        switch kind {
        case let .included(destination):
            destination.catalogFamilyText.map {
                UiMessages.string($0, locale: .english)
            } ?? destination.rawValue
        case let .excluded(metadata):
            metadata.family
        }
    }

    public var presentationFamilyText: UiVerbatimText? {
        guard case let .included(destination) = kind else { return nil }
        return destination.catalogFamilyText
    }

    public func matches(_ webPath: String) -> Bool {
        patterns.contains { $0.matches(webPath) }
    }

    public func match(_ webPath: String) -> NativeRouteMatch? {
        patterns.lazy.compactMap { $0.match(webPath) }.first
    }

    public static func included(
        destinationIdentifier: NativeRouteDestinationIdentifier,
        representativePath: String,
        patterns: [NativeRoutePattern]
    ) -> Self {
        Self(
            representativePath: representativePath,
            patterns: patterns,
            kind: .included(destinationIdentifier)
        )
    }

    public static func excluded(
        auditFamily: String,
        auditReason: String,
        representativePath: String,
        patterns: [NativeRoutePattern]
    ) -> Self {
        Self(
            representativePath: representativePath,
            patterns: patterns,
            kind: .excluded(.init(family: auditFamily, reason: auditReason))
        )
    }
}

private extension NativeRouteDestinationIdentifier {
    var catalogFamilyText: UiVerbatimText? {
        nativeRows.first?.titleText
    }
}

public enum NativeRouteCatalog {
    public static let entries: [NativeRouteCatalogEntry] =
        NativeRouteCatalogFeedsAndPosts.entries
            + NativeRouteCatalogEntities.entries
            + NativeRouteCatalogStaff.entries
            + NativeRouteCatalogAccount.entries
            + NativeRouteCatalogExcluded.entries

    public static var includedEntries: [NativeRouteCatalogEntry] {
        entries.filter {
            if case .included = $0.kind {
                true
            } else {
                false
            }
        }
    }

    public static var excludedEntries: [NativeRouteCatalogEntry] {
        entries.filter {
            if case .excluded = $0.kind {
                true
            } else {
                false
            }
        }
    }

    public static func matchingEntry(for webPath: String) -> NativeRouteCatalogEntry? {
        entries.first { $0.matches(webPath) }
    }

    public static func matchingRoute(
        for webPath: String
    ) -> (entry: NativeRouteCatalogEntry, match: NativeRouteMatch)? {
        for entry in entries {
            if let match = entry.match(webPath) {
                return (entry, match)
            }
        }
        return nil
    }
}
