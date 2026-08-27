import VouchaLocalization

public struct NativeRouteFamilyGroup: Identifiable, Hashable, Sendable {
    public let id: String
    public let title: UiMessage
    public let summary: UiMessage
    public let icon: String
    public let entries: [NativeRouteCatalogEntry]
    public let requiredRoles: [String]

    public init(
        title: UiMessage,
        summary: UiMessage,
        icon: String,
        entries: [NativeRouteCatalogEntry],
        requiredRoles: [String] = []
    ) {
        id = title.key.rawValue
        self.title = title
        self.summary = summary
        self.icon = icon
        self.entries = entries
        self.requiredRoles = requiredRoles
    }

    public static func == (lhs: Self, rhs: Self) -> Bool {
        lhs.id == rhs.id
    }

    public func hash(into hasher: inout Hasher) {
        hasher.combine(id)
    }
}

public extension AppSection {
    var nativeParityGroups: [NativeRouteFamilyGroup] {
        nativeParityGroups(featureFlags: [:])
    }

    func nativeParityGroups(featureFlags: [String: Bool]) -> [NativeRouteFamilyGroup] {
        NativeParityCatalog.groupsBySection(featureFlags: featureFlags)[self] ?? []
    }
}
